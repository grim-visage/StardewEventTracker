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
        private static readonly Regex MailCommand = new(@"(?:^|/)\s*(?:mail|addMailReceived|mailReceived|addWorldState)\s+(\S+)", RegexOptions.IgnoreCase);
        private static readonly Regex TopicCommand = new(@"(?:^|/)\s*addConversationTopic\s+(\S+)(?:\s+(\d+))?", RegexOptions.IgnoreCase);

        /// <summary>A letter that starts a conversation topic when read: "%item conversationTopic &lt;id&gt; &lt;days&gt; %%".</summary>
        private static readonly Regex LetterTopic = new(@"%item\s+conversationTopic\s+(\S+)\s+(\d+)", RegexOptions.IgnoreCase);

        private readonly Dictionary<string, string> letterTitles = new();
        private readonly Dictionary<string, (string Name, string? Objective)> specialOrders = new();
        private readonly Dictionary<string, string> triggerConditions = new();
        private readonly Dictionary<string, string> flagEvents = new();
        private readonly Dictionary<string, TopicSource> topics = new();

        /// <summary>Re-reads letters, special orders and trigger actions. Event scripts are added with <see cref="ScanScript"/>.</summary>
        public void Rebuild(IMonitor monitor)
        {
            this.letterTitles.Clear();
            this.specialOrders.Clear();
            this.triggerConditions.Clear();
            this.flagEvents.Clear();
            this.topics.Clear();

            Try(monitor, "letters", () =>
            {
                foreach ((string id, string text) in DataLoader.Mail(Game1.content))
                {
                    // letters end with "[#]Title"
                    int titleStart = text.LastIndexOf("[#]", StringComparison.Ordinal);
                    string? title = titleStart >= 0 && titleStart + 3 < text.Length ? text[(titleStart + 3)..].Trim() : null;
                    if (title != null)
                        this.letterTitles[id] = title;

                    foreach (Match match in LetterTopic.Matches(text))
                        this.topics.TryAdd(match.Groups[1].Value, new TopicSource(match.Groups[1].Value, null, title ?? id, int.Parse(match.Groups[2].Value)));
                }
            });

            Try(monitor, "special orders", () =>
            {
                foreach ((string id, SpecialOrderData order) in DataLoader.SpecialOrders(Game1.content))
                {
                    string name = Parse(order.Name) ?? id;
                    string? objective = order.Objectives?.Count == 1 ? Parse(order.Objectives[0].Text)?.Replace("{0}", order.Objectives[0].RequiredCount) : null;
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
        public void ScanScript(EventInfo evt, string script)
        {
            foreach (Match match in MailCommand.Matches(script))
                this.flagEvents.TryAdd(match.Groups[1].Value, evt.Key);
            foreach (Match match in TopicCommand.Matches(script))
            {
                int days = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out int d) ? d : 4;
                this.topics.TryAdd(match.Groups[1].Value, new TopicSource(match.Groups[1].Value, evt.Key, null, days));
            }
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
