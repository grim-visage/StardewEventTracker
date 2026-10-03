using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.SpecialOrders;
using StardewValley.TokenizableStrings;

namespace StardewEventTracker.Data
{
    /// <summary>Where a mail flag comes from: a letter, a special order reward, a trigger action or an event.</summary>
    internal sealed record FlagSource(
        string Flag,
        string? LetterTitle,
        string? SpecialOrderName,
        string? SpecialOrderObjective,
        string? TriggerCondition,
        string? EventKey);

    /// <summary>An event or letter that starts a conversation topic, and for how many days.</summary>
    internal sealed record TopicSource(string Topic, string? EventKey, string? LetterTitle, int Days);

    /// <summary>
    /// Explains mail flags and conversation topics in event requirements, from the game's own data (letters, special
    /// orders, trigger actions and event scripts), so they read as things to do instead of internal IDs. This works
    /// for any mod and stays in step with the installed versions.
    /// </summary>
    internal sealed class FlagSources
    {
        // event commands are separated by '/', so captured IDs stop there
        private static readonly Regex MailCommand = new(@"(?:^|/)\s*(?:mail|addMailReceived|mailReceived|addWorldState|action\s+AddMail\s+\S+)\s+([^\s/]+)", RegexOptions.IgnoreCase);
        private static readonly Regex TopicCommand = new(@"(?:^|/)\s*addConversationTopic\s+([^\s/]+)(?:\s+(\d+))?", RegexOptions.IgnoreCase);

        /// <summary>An event branch: "fork &lt;id&gt;" or "fork &lt;requirement&gt; &lt;id&gt;".</summary>
        private static readonly Regex ForkCommand = new(@"(?:^|/)\s*fork\s+(?:[^\s/]+\s+)?([^\s/]+)", RegexOptions.IgnoreCase);

        /// <summary>Special order text that refers to the game's string table: "[key]".</summary>
        private static readonly Regex OrderStringKey = new(@"\[([^\[\]\s]+)\]");

        /// <summary>A letter that starts a conversation topic when read: "%item conversationTopic &lt;id&gt; &lt;days&gt; %%".</summary>
        private static readonly Regex LetterTopic = new(@"%item\s+conversationTopic\s+(\S+)\s+(\d+)", RegexOptions.IgnoreCase);

        private readonly Dictionary<string, string> letterTitles = new();
        private readonly Dictionary<string, (string Name, string? Objective)> specialOrders = new();
        private readonly Dictionary<string, string> triggerConditions = new();
        private readonly Dictionary<string, string> flagEvents = new();
        private readonly Dictionary<string, TopicSource> topics = new();

        /// <summary>The event that branches into each fork script, by location and fork key.</summary>
        private readonly Dictionary<(string Location, string Fork), string> forkParents = new();

        /// <summary>Fork scripts, which can set flags on behalf of the event that branches into them.</summary>
        private readonly List<(string Location, string Key, string Script)> forkScripts = new();

        /// <summary>Re-reads letters, special orders and trigger actions. Event scripts are added with <see cref="ScanScript"/>.</summary>
        public void Rebuild(IMonitor monitor)
        {
            this.letterTitles.Clear();
            this.specialOrders.Clear();
            this.triggerConditions.Clear();
            this.flagEvents.Clear();
            this.topics.Clear();
            this.forkParents.Clear();
            this.forkScripts.Clear();

            Try(monitor, "letters", () =>
            {
                foreach ((string id, string text) in DataLoader.Mail(Game1.content))
                {
                    // letters end with "[#]Title"
                    int titleStart = text.LastIndexOf("[#]", StringComparison.Ordinal);
                    string? title = titleStart >= 0 && titleStart + 3 < text.Length ? text[(titleStart + 3)..].Trim() : null;
                    // skip titles a mod forgot to translate
                    if (title != null && !title.Contains("no translation", StringComparison.OrdinalIgnoreCase))
                        this.letterTitles[id] = title;

                    foreach (Match match in LetterTopic.Matches(text))
                        this.topics.TryAdd(match.Groups[1].Value, new TopicSource(match.Groups[1].Value, null, title ?? id, int.Parse(match.Groups[2].Value)));
                }
            });

            Try(monitor, "special orders", () =>
            {
                foreach ((string id, SpecialOrderData order) in DataLoader.SpecialOrders(Game1.content))
                {
                    string name = ParseOrderText(order.Name) ?? id;
                    string? objective = order.Objectives?.Count == 1 ? ParseOrderText(order.Objectives[0].Text)?.Replace("{0}", order.Objectives[0].RequiredCount) : null;
                    foreach (SpecialOrderRewardData reward in order.Rewards ?? new List<SpecialOrderRewardData>())
                    {
                        if (reward.Type == "Mail" && reward.Data?.TryGetValue("MailReceived", out string? flags) == true)
                        {
                            foreach (string flag in flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                this.specialOrders.TryAdd(flag, (name, objective));
                        }
                    }
                }
            });

            Try(monitor, "trigger actions", () =>
            {
                foreach (TriggerActionData trigger in DataLoader.TriggerActions(Game1.content))
                {
                    var actions = (trigger.Actions ?? new List<string>()).Append(trigger.Action).Where(a => !string.IsNullOrWhiteSpace(a));
                    foreach (string action in actions)
                    {
                        // AddMail <player> <mail ID> [type]
                        string[] args = ArgUtility.SplitBySpace(action);
                        if (args.Length >= 3 && args[0].Equals("AddMail", StringComparison.OrdinalIgnoreCase))
                            this.triggerConditions.TryAdd(args[2], trigger.Condition ?? "");
                    }
                }
            });
        }

        /// <summary>Records the mail flags and conversation topics an event's script sets.</summary>
        public void ScanScript(EventInfo evt, string script) => this.ScanScript(evt.Key, evt.LocationName, script);

        /// <summary>Records a fork script (an event branch), credited to its parent event by <see cref="ResolveForks"/>.</summary>
        public void AddFork(string location, string key, string script) => this.forkScripts.Add((location, key, script));

        /// <summary>Credits what each fork script sets to the event that branches into it (forks can branch again).</summary>
        public void ResolveForks()
        {
            var done = new HashSet<(string, string)>();
            for (int pass = 0; pass < 4; pass++)
            {
                foreach ((string location, string key, string script) in this.forkScripts)
                {
                    if (!done.Contains((location, key)) && this.forkParents.TryGetValue((location, key), out string? parent))
                    {
                        done.Add((location, key));
                        this.ScanScript(parent, location, script);
                    }
                }
            }
        }

        private void ScanScript(string eventKey, string location, string script)
        {
            foreach (Match match in MailCommand.Matches(script))
                this.flagEvents.TryAdd(match.Groups[1].Value, eventKey);
            foreach (Match match in TopicCommand.Matches(script))
            {
                int days = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out int d) ? d : 4;
                this.topics.TryAdd(match.Groups[1].Value, new TopicSource(match.Groups[1].Value, eventKey, null, days));
            }
            foreach (Match match in ForkCommand.Matches(script))
                this.forkParents.TryAdd((location, match.Groups[1].Value), eventKey);
        }

        /// <summary>Special order text, looking up "[key]" in the game's special order strings like the game does.</summary>
        private static string? ParseOrderText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            text = OrderStringKey.Replace(text, m => Game1.content.LoadStringReturnNullIfNotFound($"Strings\\SpecialOrderStrings:{m.Groups[1].Value}") ?? m.Value);
            return Parse(text);
        }

        public FlagSource? GetFlag(string flag)
        {
            this.letterTitles.TryGetValue(flag, out string? title);
            this.triggerConditions.TryGetValue(flag, out string? condition);
            this.flagEvents.TryGetValue(flag, out string? eventKey);
            bool hasOrder = this.specialOrders.TryGetValue(flag, out var order);

            if (title == null && condition == null && eventKey == null && !hasOrder)
                return null;
            return new FlagSource(flag, title, hasOrder ? order.Name : null, hasOrder ? order.Objective : null, condition, eventKey);
        }

        public TopicSource? GetTopic(string topic) => this.topics.TryGetValue(topic, out TopicSource? source) ? source : null;

        private static string? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string parsed = TokenParser.ParseText(text);
            return string.IsNullOrWhiteSpace(parsed) ? null : parsed.Trim();
        }

        private static void Try(IMonitor monitor, string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                monitor.Log($"Couldn't read {what} to explain event requirements: {ex.Message}", LogLevel.Trace);
            }
        }
    }
}
