using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;

namespace StardewEventTracker.Data
{
    /// <summary>Turns event preconditions into readable text.</summary>
    internal static class PreconditionFormatter
    {
        public static string Describe(Precondition c, EventIndex index)
        {
            if (c.IsNeverTrue)
                return I18n.Get("cond.never-true");

            string[] a = c.Args;
            string all = string.Join(" ", a);
            string or = I18n.Get("join.or");
            string neg = c.Negated ? ".not" : "";
            string? text = c.Name.ToLowerInvariant() switch
            {
                "friendship" => string.Join("; ", EventInfo.FriendshipPairs(c).Select(p => DescribeFriendship(p.Npc, p.Points))),
                "time" when a.Length >= 2 => I18n.Get("cond.time", new { start = Time(a[0]), end = Time(a[1]) }),
                "weather" => I18n.Get("cond.weather", new { weather = string.Join(or, a.Select(WeatherName)) }),
                "dayofweek" => I18n.Get("cond.day-of-week" + neg, new { days = string.Join("/", a.Select(DayName)) }),
                "dayofmonth" => I18n.Get("cond.day-of-month" + neg, new { days = string.Join(", ", a) }),
                "season" => I18n.Get("cond.season" + neg, new { seasons = string.Join(or, a.Select(SeasonName)) }),
                "daysplayed" => I18n.Get("cond.days-played", new { count = all }),
                "year" => a.FirstOrDefault() == "1" ? I18n.Get("cond.year-one") : I18n.Get("cond.year", new { year = all }),
                "sawevent" => I18n.Get("cond.saw-event" + neg, new { events = string.Join(or, a.Select(index.DescribeEvent)) }),
                "dating" => I18n.Get("cond.dating", new { name = Npc(a) }),
                "spouse" => I18n.Get("cond.spouse" + neg, new { name = Npc(a) }),
                "roommate" => I18n.Get("cond.roommate" + neg, new { name = Npc(a) }),
                "hostmail" or "hostorlocalmail" or "localmail" or "worldstate" when !c.Negated && a.Length > 0 =>
                    ExplainFlag(a[0], index, forStep: false) ?? I18n.Get("cond.flag", new { flag = HumanizeFlag(a[0]) }),
                "hostmail" or "hostorlocalmail" or "localmail" => I18n.Get("cond.mail" + neg, new { flag = all }),
                "worldstate" => I18n.Get("cond.world-state" + neg, new { flag = all }),
                "hasmoney" => I18n.Get("cond.has-money", new { amount = all }),
                "freeinventoryslots" => I18n.Get("cond.free-slots", new { count = all }),
                "spousebed" => I18n.Get("cond.spouse-bed"),
                "activedialogueevent" => (a.Length > 0 ? ExplainTopic(a[0], c.Negated, index, forStep: false) : null)
                    ?? I18n.Get("cond.conversation-topic" + neg, new { topic = all }),
                "skill" when a.Length >= 2 => I18n.Get("cond.skill" + neg, new { skill = a[0], level = a[1] }),
                "tile" => a.Length >= 2 ? I18n.Get("cond.tile", new { x = a[0], y = a[1] }) : I18n.Get("cond.tile-any"),
                "ishost" => I18n.Get("cond.is-host"),
                "earnedmoney" => I18n.Get("cond.earned-money", new { amount = all }),
                "hasitem" => I18n.Get("cond.has-item", new { items = string.Join(", ", a.Select(ItemName)) }),
                "shipped" => I18n.Get("cond.shipped", new { items = DescribeShipped(a) }),
                "random" => double.TryParse(a.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double chance)
                    ? I18n.Get("cond.random", new { percent = (chance * 100).ToString("0.#", CultureInfo.InvariantCulture) })
                    : I18n.Get("cond.random-any"),
                "sendmail" => I18n.Get("cond.send-mail"),
                "gamestatequery" => I18n.Get("cond.game-state-query", new { query = all }),
                "gender" => I18n.Get("cond.gender", new { gender = all }),
                "festivalday" => I18n.Get("cond.festival-day" + neg),
                "upcomingfestival" => I18n.Get("cond.upcoming-festival" + neg, new { days = all }),
                "goldenwalnuts" => I18n.Get("cond.golden-walnuts", new { count = all }),
                "reachedminebottom" => I18n.Get("cond.mine-bottom"),
                "communitycenterorwarehousedone" => I18n.Get("cond.cc-or-joja-done" + neg),
                "jojabundlesdone" => I18n.Get("cond.joja-done"),
                "inupgradedhouse" => I18n.Get("cond.house-upgrade", new { level = a.Length > 0 ? all : "1" }),
                "npcvisiblehere" => I18n.Get("cond.npc-here", new { name = Npc(a) }),
                "npcvisible" => I18n.Get("cond.npc-around", new { name = Npc(a) }),
                "sawsecretnote" => I18n.Get("cond.secret-note", new { note = all }),
                "chosedialogueanswers" => I18n.Get("cond.dialogue-answer", new { answer = all }),
                "missingpet" => I18n.Get("cond.pet"),
                _ => null
            };

            // e.g. a requirement another mod registered with the game
            if (text == null)
                return Hints.ForPrecondition(c.Name) is { } hint ? hint.Text : I18n.Get("cond.other", new { raw = c.Raw });

            // conditions whose text above doesn't already reflect negation
            bool handlesNegation = c.Name.ToLowerInvariant() is "dayofweek" or "dayofmonth" or "season" or "sawevent"
                or "spouse" or "roommate" or "hostmail" or "hostorlocalmail" or "localmail" or "festivalday" or "upcomingfestival"
                or "worldstate" or "activedialogueevent" or "skill" or "communitycenterorwarehousedone";
            return c.Negated && !handlesNegation ? I18n.Get("cond.negated", new { text }) : text;
        }

        /// <summary>
        /// What a mail flag means, from the game's own data: a letter to receive, a special order to complete, a
        /// trigger condition to meet, or an event that sets it. Null if nothing explains it.
        /// </summary>
        /// <param name="forStep">Use the short lower-case form for "Not yet: ..." lines.</param>
        public static string? ExplainFlag(string flag, EventIndex index, bool forStep, int depth = 0)
        {
            FlagSource? source = index.Flags.GetFlag(flag);
            string prefix = forStep ? "step" : "flag";

            // a hand-written hint says it best; otherwise work it out from the data
            if (Hints.ForFlag(flag) is { } hint)
            {
                string text = forStep && hint.Text.Length > 0 ? char.ToLowerInvariant(hint.Text[0]) + hint.Text[1..] : hint.Text;
                return hint.Source != null && !forStep
                    ? I18n.Get("flag.hint-source", new { hint = text, source = hint.Source })
                    : text;
            }
            if (source == null)
                return null;

            if (source.SpecialOrderName != null)
            {
                return source.SpecialOrderObjective != null
                    ? I18n.Get($"{prefix}.special-order-objective", new { name = source.SpecialOrderName, objective = source.SpecialOrderObjective })
                    : I18n.Get($"{prefix}.special-order", new { name = source.SpecialOrderName });
            }

            if (source.QuestTitle != null)
                return I18n.Get($"{prefix}.quest", new { title = source.QuestTitle });

            string? when = source.TriggerCondition != null && depth == 0 ? DescribeCondition(source.TriggerCondition, index) : null;
            if (source.LetterTitle != null)
            {
                return when != null
                    ? I18n.Get($"{prefix}.letter-when", new { title = source.LetterTitle, when })
                    : I18n.Get($"{prefix}.letter", new { title = source.LetterTitle });
            }
            if (when != null)
                return I18n.Get($"{prefix}.trigger", new { when });
            if (source.EventKey != null && index.FindByKey(source.EventKey) is { } evt)
                return I18n.Get($"{prefix}.from-event", new { @event = index.DescribeEventShort(evt.Id) });
            if (source.DialogueNpc != null)
                return I18n.Get($"{prefix}.dialogue", new { name = EventIndex.GetNpcDisplayName(source.DialogueNpc) });
            return null;
        }

        /// <summary>A conversation-topic requirement as a wait: "Wait 4 days after Cirrus's 1-heart event". Null if unknown.</summary>
        public static string? ExplainTopic(string topic, bool negated, EventIndex index, bool forStep)
        {
            if (index.Flags.GetTopic(topic) is not { } source)
            {
                // e.g. the game's own topics, started by its code: use a researched hint if there is one
                if (Hints.ForTopic(topic) is not { } hint)
                    return null;
                return I18n.Get(forStep
                    ? (negated ? "step.topic-wait-hint" : "step.topic-within-hint")
                    : (negated ? "topic.topic-wait-hint" : "topic.topic-within-hint"), new { cause = hint.Text });
            }

            string? after = source.EventKey != null && index.FindByKey(source.EventKey) is { } evt
                ? index.DescribeEventShort(evt.Id)
                : source.LetterTitle != null ? I18n.Get("topic.letter", new { title = source.LetterTitle })
                : source.QuestTitle != null ? I18n.Get("topic.quest", new { title = source.QuestTitle }) : null;
            if (after == null)
                return null;

            // 0 days lasts for the rest of that day; 1 day reads "1 day"
            string prefix = forStep ? "step" : "topic";
            string kind = negated ? "topic-wait" : "topic-within";
            string count = source.Days switch { <= 0 => ".0", 1 => ".1", _ => "" };
            return I18n.Get($"{prefix}.{kind}{count}", new { days = source.Days, @event = after });
        }

        /// <summary>
        /// Turns a game state query (from a trigger action) into readable clauses, e.g. "have Cherry Pit" or "have 8
        /// hearts with Dale and have seen Dale's 6-heart event". Unrecognized clauses are left out; null if none remain.
        /// </summary>
        public static string? DescribeCondition(string condition, EventIndex index)
        {
            var clauses = new List<string>();
            foreach (string rawClause in condition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                bool negated = rawClause.StartsWith('!');
                string[] t = ArgUtility.SplitBySpaceQuoteAware(negated ? rawClause[1..] : rawClause);
                if (t.Length == 0)
                    continue;

                string neg = negated ? ".not" : "";
                string? clause = t[0].ToUpperInvariant() switch
                {
                    // ANY "query" "query": any one of them
                    "ANY" when !negated => t.Skip(1).Select(q => DescribeCondition(q, index)).OfType<string>().Distinct().ToList() is { Count: > 0 } any
                        ? string.Join(I18n.Get("join.or"), any)
                        : null,
                    "PLAYER_HAS_ITEM" when t.Length >= 3 => I18n.Get("gsq.has-item" + neg, new { item = ItemName(t[2]) }),
                    "PLAYER_FRIENDSHIP_POINTS" when !negated && t.Length >= 4 && int.TryParse(t[3], out int points) =>
                        points % NPC.friendshipPointsPerHeartLevel == 0
                            ? I18n.Get("gsq.hearts", new { hearts = points / NPC.friendshipPointsPerHeartLevel, name = EventIndex.GetNpcDisplayName(t[2]) })
                            : I18n.Get("gsq.points", new { points, name = EventIndex.GetNpcDisplayName(t[2]) }),
                    "PLAYER_HEARTS" when !negated && t.Length >= 4 => I18n.Get("gsq.hearts", new { hearts = t[3], name = EventIndex.GetNpcDisplayName(t[2]) }),
                    "PLAYER_HAS_SEEN_EVENT" when !negated && t.Length >= 3 => index.FindById(t[2]) != null
                        ? I18n.Get("gsq.seen-event", new { @event = index.DescribeEventShort(t[2]) })
                        : null,
                    "PLAYER_HAS_MAIL" when !negated && t.Length >= 3 => ExplainFlag(t[2], index, forStep: true, depth: 1),

                    // secret notes from 1000 up are Ginger Island's journal scraps
                    "PLAYER_HAS_SECRET_NOTE" when !negated && t.Length >= 3 && int.TryParse(t[2], out int note) => note >= 1000
                        ? I18n.Get("gsq.journal-scrap", new { number = note - 1000 })
                        : I18n.Get("gsq.secret-note", new { number = note }),
                    "PLAYER_NPC_RELATIONSHIP" when !negated && t.Length >= 4 => I18n.Get("gsq.relationship", new
                    {
                        types = string.Join(I18n.Get("join.or"), t.Skip(3).Select(type => I18n.GetOr($"relationship.{type.ToLowerInvariant()}", type.ToLowerInvariant()))),
                        name = EventIndex.GetNpcDisplayName(t[2])
                    }),
                    "SEASON" when t.Length >= 2 => I18n.Get("gsq.season" + neg, new { seasons = string.Join(I18n.Get("join.or"), t.Skip(1).Select(SeasonName)) }),
                    _ => null
                };
                if (clause != null && !clauses.Contains(clause))
                    clauses.Add(clause);
            }

            // keep it readable: the first few conditions say enough
            if (clauses.Count > 3)
                clauses = clauses.Take(3).Append(I18n.Get("gsq.more")).ToList();
            return clauses.Count > 0 ? string.Join(I18n.Get("join.and"), clauses) : null;
        }

        /// <summary>A flag ID as words: "HasCherryPitInInventory" -> "Has cherry pit in inventory".</summary>
        public static string HumanizeFlag(string flag)
        {
            string words = Regex.Replace(flag.Replace('_', ' ').Replace('.', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ").Trim();
            words = Regex.Replace(words, @"\s+", " ");
            return words.Length > 0 ? char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant() : flag;
        }

        /// <summary>The time window as a schedule, e.g. "9:00 am-12:00 pm, starts in 1h 20m".</summary>
        public static string DescribeTimeWindow(EventInfo evt, EventEvaluation eval)
        {
            if (evt.Window is not { } window)
                return I18n.Get("time.some-times");

            var tokens = new
            {
                range = I18n.Get("time.range", new { start = Time(window.Start), end = Time(window.End) }),
                end = Time(window.End),
                duration = FormatDuration(eval.MinutesUntilStart ?? 0)
            };
            // a locked door or festival can push the start later than the event's own window
            if (eval.MinutesUntilStart > 0)
                return I18n.Get("time.starts-in", tokens);
            if (eval.TimeOpen)
                return I18n.Get("time.open", tokens);
            return I18n.Get("time.over", tokens);
        }

        /// <summary>Formats in-game minutes as "1h 20m", "2h" or "40m".</summary>
        public static string FormatDuration(int minutes)
        {
            minutes = Math.Max(0, minutes);
            int hours = minutes / 60, rest = minutes % 60;
            if (hours == 0)
                return I18n.Get("duration.minutes", new { minutes = rest });
            return rest == 0
                ? I18n.Get("duration.hours", new { hours })
                : I18n.Get("duration.hours-minutes", new { hours, minutes = rest });
        }

        /// <summary>A friendship requirement, plus how much is left if it isn't met: "4 hearts with Leah (2 more hearts to go)".</summary>
        public static string DescribeFriendship(string npc, int points)
        {
            string name = EventIndex.GetNpcDisplayName(npc);
            bool isHearts = points % NPC.friendshipPointsPerHeartLevel == 0;
            string requirement = isHearts
                ? I18n.Get("cond.hearts", new { hearts = points / NPC.friendshipPointsPerHeartLevel, name })
                : I18n.Get("cond.points", new { points, name });

            string? more = MoreFriendship(npc, points);
            return more == null ? requirement : I18n.Get("cond.friendship-to-go", new { requirement, more });
        }

        /// <summary>
        /// How much more friendship is needed to reach a requirement, e.g. "1 more heart" or "120 more points";
        /// null if it's already met. A heart counts as reached at its threshold, so 7.6 hearts needs "1 more heart" for 8.
        /// </summary>
        public static string? MoreFriendship(string npc, int requiredPoints)
        {
            int missing = requiredPoints - Game1.player.getFriendshipLevelForNPC(npc);
            if (missing <= 0)
                return null;

            int perHeart = NPC.friendshipPointsPerHeartLevel;
            if (requiredPoints % perHeart != 0)
                return I18n.Get(missing == 1 ? "friendship.more-point" : "friendship.more-points", new { count = missing });

            int hearts = (int)Math.Ceiling(missing / (double)perHeart);
            return I18n.Get(hearts == 1 ? "friendship.more-heart" : "friendship.more-hearts", new { count = hearts });
        }

        /// <summary>A weather value from an event condition, translated where known ("sunny" -> "Sunny").</summary>
        public static string WeatherName(string raw) => I18n.GetOr($"weather.{raw.ToLowerInvariant()}", Capitalize(raw));

        public static string SeasonName(string raw) => I18n.GetOr($"season.{raw.ToLowerInvariant()}", Capitalize(raw));

        public static string DayName(string raw) => I18n.GetOr($"day.{raw.ToLowerInvariant()}", Capitalize(raw));

        public static string Time(int time) => Game1.getTimeOfDayString(time);

        private static string Time(string raw) =>
            int.TryParse(raw, out int time) ? Time(time) : raw;

        private static string Npc(string[] args) =>
            args.Length > 0 ? EventIndex.GetNpcDisplayName(args[0]) : I18n.Get("someone");

        private static string Capitalize(string s) =>
            s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;

        private static string ItemName(string id)
        {
            try
            {
                return ItemRegistry.GetDataOrErrorItem(id).DisplayName;
            }
            catch (Exception)
            {
                return id;
            }
        }

        private static string DescribeShipped(string[] args)
        {
            var parts = new List<string>();
            for (int i = 0; i + 1 < args.Length; i += 2)
                parts.Add(I18n.Get("cond.shipped-item", new { count = args[i + 1], item = ItemName(args[i]) }));
            return parts.Count > 0 ? string.Join(", ", parts) : string.Join(" ", args);
        }
    }
}
