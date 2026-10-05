using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Museum;
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
        string? EventKey,
        string? QuestTitle = null,
        string? DialogueNpc = null);

    /// <summary>An event or letter that starts a conversation topic, and for how many days.</summary>
    internal sealed record TopicSource(string Topic, string? EventKey, string? LetterTitle, int Days, string? QuestTitle = null);

    /// <summary>
    /// Explains mail flags and conversation topics in event requirements, from the game's own data (letters, special
    /// orders, trigger actions and event scripts), so they read as things to do instead of internal IDs. This works
    /// for any mod and stays in step with the installed versions.
    /// </summary>
    internal sealed class FlagSources
    {
        // event commands are separated by '/', or by '\' and "(break)" inside a question's answers, so IDs stop there
        private const string CommandStart = @"(?:^|[/\\]|\(break\))\s*";
        private const string Id = @"([^\s/\\(]+)";
        private static readonly Regex MailCommand = new(CommandStart + @"(?:mail|addMailReceived|mailReceived|addWorldState|action\s+AddMail\s+\S+)\s+" + Id, RegexOptions.IgnoreCase);
        private static readonly Regex TopicCommand = new(CommandStart + @"addConversationTopic\s+" + Id + @"(?:\s+(\d+))?", RegexOptions.IgnoreCase);

        /// <summary>An event branch: "fork &lt;id&gt;", "fork &lt;requirement&gt; &lt;id&gt;" or "switchEvent &lt;id&gt;".</summary>
        private static readonly Regex ForkCommand = new(CommandStart + @"(?:fork\s+(?:[^\s/\\(]+\s+)?|switchEvent\s+)" + Id, RegexOptions.IgnoreCase);

        /// <summary>Dialogue shown once, which sets a flag: "$1 &lt;flag&gt;#&lt;text&gt;".</summary>
        private static readonly Regex OnceDialogue = new(@"\$1\s+([^\s#]+)#");

        /// <summary>Flags and topics the game's own code sets when you finish a quest (Quest.questComplete).</summary>
        private static readonly Dictionary<string, (string QuestId, int TopicDays)> CodeQuestFlags = new()
        {
            ["emilyFiber"] = ("126", 2)
        };

        /// <summary>Special order text that refers to the game's string table: "[key]".</summary>
        private static readonly Regex OrderStringKey = new(@"\[([^\[\]\s]+)\]");

        /// <summary>A letter that starts a conversation topic when read: "%item conversationTopic &lt;id&gt; &lt;days&gt; %%".</summary>
        private static readonly Regex LetterTopic = new(@"%item\s+conversationTopic\s+(\S+)\s+(\d+)", RegexOptions.IgnoreCase);

        private readonly Dictionary<string, string> letterTitles = new();
        private readonly Dictionary<string, (string Name, string? Objective)> specialOrders = new();
        private readonly Dictionary<string, string> triggerConditions = new();

        /// <summary>
        /// Event IDs that aren't real events but markers a trigger action sets with MarkEventSeen (e.g. East Scarp
        /// marks one seen at the end of the day you see another), with the trigger and its condition.
        /// </summary>
        private readonly Dictionary<string, (string Trigger, string Condition)> eventMarkers = new();

        /// <summary>Event-ID markers the museum sets as a donation reward, with the number of donations it needs (the Rusty Key's at 60).</summary>
        private readonly Dictionary<string, int> museumMarkers = new();

        /// <summary>Event-ID markers the game's own code sets, and the translation key saying what does it.</summary>
        private static readonly Dictionary<string, string> CodeMarkers = new()
        {
            ["321777"] = "marker.grandpa-reevaluation"
        };

        /// <summary>A trigger condition's "has seen event" clause: PLAYER_HAS_SEEN_EVENT &lt;player&gt; &lt;event ID&gt;.</summary>
        private static readonly Regex SeenEventClause = new(@"(?:^|,)\s*PLAYER_HAS_SEEN_EVENT\s+\S+\s+(\S+)", RegexOptions.IgnoreCase);
        /// <summary>The event that sets each flag, and whether only a branch of it does (one answer to a question, or a conditional fork).</summary>
        private readonly Dictionary<string, (string EventKey, bool BranchOnly)> flagEvents = new();
        private readonly Dictionary<string, string> questFlags = new();
        private readonly Dictionary<string, string> dialogueFlags = new();
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
            this.eventMarkers.Clear();
            this.museumMarkers.Clear();
            this.flagEvents.Clear();
            this.questFlags.Clear();
            this.dialogueFlags.Clear();
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
                    {
                        // an out-of-range day count would otherwise stop the scan of every letter after it
                        if (int.TryParse(match.Groups[2].Value, out int days))
                            this.topics.TryAdd(match.Groups[1].Value, new TopicSource(match.Groups[1].Value, null, title ?? id, days));
                    }
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

            Try(monitor, "quests", () =>
            {
                // type/title/description/objective/requirements/next/money/reward/cancellable/reaction text
                Dictionary<string, string> quests = DataLoader.Quests(Game1.content);
                foreach ((string id, string data) in quests)
                {
                    string title = Parse(ArgUtility.Get(data.Split('/'), 1)) ?? id;
                    foreach (Match match in OnceDialogue.Matches(data))
                        this.questFlags.TryAdd(match.Groups[1].Value, title);
                }
                foreach ((string flag, (string questId, int days)) in CodeQuestFlags)
                {
                    if (quests.TryGetValue(questId, out string? data) && Parse(ArgUtility.Get(data.Split('/'), 1)) is { } title)
                    {
                        this.questFlags.TryAdd(flag, title);
                        this.topics.TryAdd(flag, new TopicSource(flag, null, null, days, title));
                    }
                }
            });

            Try(monitor, "dialogue", () =>
            {
                foreach (string npc in Game1.characterData.Keys)
                {
                    string asset = "Characters\\Dialogue\\" + npc;
                    if (!Game1.content.DoesAssetExist<Dictionary<string, string>>(asset))
                        continue;
                    foreach (string line in Game1.content.Load<Dictionary<string, string>>(asset).Values)
                    {
                        foreach (Match match in OnceDialogue.Matches(line))
                            this.dialogueFlags.TryAdd(match.Groups[1].Value, npc);
                    }
                }
            });

            Try(monitor, "museum rewards", () =>
            {
                foreach (MuseumRewards reward in DataLoader.MuseumRewards(Game1.content).Values)
                {
                    int count = reward.TargetContextTags?.Where(t => string.IsNullOrWhiteSpace(t.Tag)).Sum(t => t.Count) ?? 0;
                    foreach (string action in reward.RewardActions ?? new List<string>())
                    {
                        // MarkEventSeen <player> <event ID> [seen]
                        string[] args = ArgUtility.SplitBySpace(action);
                        if (count > 0 && args.Length >= 3 && args[0].Equals("MarkEventSeen", StringComparison.OrdinalIgnoreCase))
                            this.museumMarkers.TryAdd(args[2], count);
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

                        // MarkEventSeen <player> <event ID> [seen]
                        if (args.Length >= 3 && args[0].Equals("MarkEventSeen", StringComparison.OrdinalIgnoreCase) && (args.Length < 4 || !args[3].Equals("false", StringComparison.OrdinalIgnoreCase)))
                            this.eventMarkers.TryAdd(args[2], (trigger.Trigger ?? "", trigger.Condition ?? ""));
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
                        this.ScanScript(parent, location, script, branch: true);
                    }
                }
            }
        }

        private void ScanScript(string eventKey, string location, string script, bool branch = false)
        {
            // after a fork, the rest of the script only runs if the event didn't switch to the branch (e.g. the other answer)
            int firstFork = ForkCommand.Match(script) is { Success: true } fork ? fork.Index : int.MaxValue;

            // an event that always sets the flag explains it better than one that only sets it on some paths
            foreach (Match match in MailCommand.Matches(script))
            {
                string flag = match.Groups[1].Value;
                bool onePath = branch || match.Index > firstFork;
                if (!this.flagEvents.TryGetValue(flag, out var known) || (known.BranchOnly && !onePath))
                    this.flagEvents[flag] = (eventKey, onePath);
            }
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
            string? eventKey = this.flagEvents.TryGetValue(flag, out var fromEvent) ? fromEvent.EventKey : null;
            this.questFlags.TryGetValue(flag, out string? quest);
            this.dialogueFlags.TryGetValue(flag, out string? npc);
            bool hasOrder = this.specialOrders.TryGetValue(flag, out var order);

            if (title == null && condition == null && eventKey == null && quest == null && npc == null && !hasOrder)
                return null;
            return new FlagSource(flag, title, hasOrder ? order.Name : null, hasOrder ? order.Objective : null, condition, eventKey, quest, npc);
        }

        /// <summary>
        /// Whether a flag can no longer be received: only one path through an event sets it (an answer to a question,
        /// or a conditional branch), the player has seen that event without getting the flag, and nothing else sets it.
        /// Events only play once, so the other path was taken for good.
        /// </summary>
        public bool IsMissedChoice(string flag, Farmer player)
        {
            if (!this.flagEvents.TryGetValue(flag, out var source) || !source.BranchOnly || player.mailReceived.Contains(flag))
                return false;

            // the event's "mail" command sends a letter, which sets the flag once it's read
            if (player.mailbox.Contains(flag) || player.mailForTomorrow.Any(m => m == flag || m.StartsWith(flag + "%&NL&%")))
                return false;
            if (this.letterTitles.ContainsKey(flag) || this.triggerConditions.ContainsKey(flag) || this.questFlags.ContainsKey(flag)
                || this.dialogueFlags.ContainsKey(flag) || this.specialOrders.ContainsKey(flag) || Hints.ForFlag(flag) != null)
                return false;

            // event keys are "<location>|<event ID>"
            string eventId = source.EventKey[(source.EventKey.IndexOf('|') + 1)..];
            return player.eventsSeen.Contains(eventId);
        }

        /// <summary>The trigger that marks this event ID seen, and its condition, if it's a marker rather than a real event.</summary>
        public (string Trigger, string Condition)? GetEventMarker(string eventId) =>
            this.eventMarkers.TryGetValue(eventId, out var marker) ? marker : null;

        /// <summary>
        /// What sets an event-ID marker that isn't a trigger action, as a clause ("you've donated 60 items to the
        /// museum"): a museum reward or the game's own code. Null if neither does.
        /// </summary>
        public string? DescribeOtherMarker(string eventId)
        {
            if (this.museumMarkers.TryGetValue(eventId, out int count))
                return I18n.Get("marker.museum", new { count });
            return CodeMarkers.TryGetValue(eventId, out string? key) ? I18n.Get(key) : null;
        }

        /// <summary>The events a marker's trigger waits for the player to have seen, e.g. the event whose day-end marks it.</summary>
        public IEnumerable<string> GetMarkerSources(string eventId) =>
            this.eventMarkers.TryGetValue(eventId, out var marker)
                ? SeenEventClause.Matches(marker.Condition).Select(m => m.Groups[1].Value).Where(id => id != eventId)
                : Enumerable.Empty<string>();

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
