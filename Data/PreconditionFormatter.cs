using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace StardewEventTracker.Data
{
    /// <summary>Turns event preconditions into readable text.</summary>
    internal static class PreconditionFormatter
    {
        /// <param name="evt">The event the condition belongs to, to name its location where that reads better than "this location".</param>
        public static string Describe(Precondition c, EventIndex index, EventInfo? evt = null)
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
                // the game only reads the first weather and season listed, so say which others it ignores
                "weather" when a.Length > 0 => I18n.Get("cond.weather", new { weather = WeatherName(a[0]) }) + Ignored(a, WeatherName),
                "dayofweek" => DescribeDays(a, c.Negated),
                "dayofmonth" => I18n.Get("cond.day-of-month" + neg, new { days = JoinList(a, or) }),
                "season" when a.Length > 0 => I18n.Get("cond.season" + neg, new { seasons = SeasonName(a[0]) }) + Ignored(a, SeasonName),
                "daysplayed" => I18n.Get("cond.days-played", new { count = all }),
                "year" => a.FirstOrDefault() == "1" ? I18n.Get("cond.year-one") : I18n.Get("cond.year", new { year = all }),
                "sawevent" => I18n.Get("cond.saw-event" + neg, new { events = JoinList(a.Select(index.DescribeEvent), or) }),
                // a mod that wrote "D" (dating) for "d" (day of week): nobody is called "Mon", so it can never be true
                "dating" or "spouse" when !c.Negated && a.Length > 0 && !IsCharacter(a[0]) => I18n.Get("cond.not-a-character", new { name = a[0] }),
                "dating" => I18n.Get("cond.dating" + neg, new { name = Npc(a) }),
                "spouse" => I18n.Get("cond.spouse" + neg, new { name = Npc(a) }),
                "roommate" => I18n.Get("cond.roommate" + neg, new { name = Npc(a) }),
                "hostmail" or "hostorlocalmail" or "localmail" or "worldstate" when !c.Negated && a.Length > 0 =>
                    ExplainFlag(a[0], index, forStep: false) ?? I18n.Get("cond.flag", new { flag = HumanizeFlag(a[0]) }),
                "hostmail" or "hostorlocalmail" or "localmail" => I18n.Get("cond.mail" + neg, new { flag = a.Length > 0 ? HumanizeFlag(a[0]) : all }),
                "worldstate" => I18n.Get("cond.world-state" + neg, new { flag = a.Length > 0 ? HumanizeFlag(a[0]) : all }),
                "hasmoney" => I18n.Get("cond.has-money", new { amount = all }),
                "freeinventoryslots" => I18n.Get("cond.free-slots", new { count = all }),
                "spousebed" => I18n.Get("cond.spouse-bed"),
                "activedialogueevent" => (a.Length > 0 ? ExplainTopic(a[0], c.Negated, index, forStep: false) : null)
                    ?? I18n.Get("cond.conversation-topic" + neg, new { topic = all }),
                "skill" when a.Length >= 2 => I18n.Get("cond.skill" + neg, new { skill = SkillName(a[0]), level = a[1] }),
                "tile" => a.Length >= 2 ? I18n.Get("cond.tile", new { x = a[0], y = a[1] }) : I18n.Get("cond.tile-any"),
                "ishost" => I18n.Get("cond.is-host"),
                "earnedmoney" => I18n.Get("cond.earned-money", new { amount = all }),
                "hasitem" => I18n.Get("cond.has-item", new { items = JoinList(a.Select(ItemName), I18n.Get("join.and")) }),
                "shipped" => I18n.Get("cond.shipped", new { items = DescribeShipped(a) }),
                "random" => double.TryParse(a.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double chance)
                    ? I18n.Get("cond.random", new { percent = (chance * 100).ToString("0.#", CultureInfo.InvariantCulture) })
                    : I18n.Get("cond.random-any"),
                "sendmail" => I18n.Get("cond.send-mail"),
                // a negated query with one clause reads as that clause negated
                "gamestatequery" => DescribeCondition(c.Negated && !all.Contains(',') ? (all.StartsWith('!') ? all[1..] : "!" + all) : all, index) is { } when
                    && (!c.Negated || !all.Contains(','))
                    ? I18n.Get("cond.game-state-query.known", new { when })
                    : I18n.Get("cond.game-state-query", new { query = all }),
                "gender" => I18n.Get("cond.gender", new { gender = all }),
                "festivalday" => I18n.Get("cond.festival-day" + neg),
                "upcomingfestival" => I18n.Get("cond.upcoming-festival" + neg, new { days = all }),
                "goldenwalnuts" => I18n.Get("cond.golden-walnuts", new { count = all }),
                "reachedminebottom" => I18n.Get("cond.mine-bottom"),
                "communitycenterorwarehousedone" => I18n.Get("cond.cc-or-joja-done" + neg),
                "jojabundlesdone" => I18n.Get("cond.joja-done"),
                "inupgradedhouse" => I18n.Get("cond.house-upgrade", new { level = a.Length > 0 ? all : "1" }),
                "npcvisiblehere" => evt != null
                    ? I18n.Get("cond.npc-at", new { name = Npc(a), place = EventNarrator.WithArticle(evt.LocationDisplayName) })
                    : I18n.Get("cond.npc-here", new { name = Npc(a) }),
                "npcvisible" => I18n.Get("cond.npc-around", new { name = Npc(a) }),
                "sawsecretnote" => I18n.Get("cond.secret-note", new { note = all }),
                "chosedialogueanswers" => I18n.Get(a.Length > 1 ? "cond.dialogue-answers" : "cond.dialogue-answer"),
                "missingpet" => a.Length > 0 ? I18n.Get("cond.pet-type", new { type = a[0].ToLowerInvariant() }) : I18n.Get("cond.pet"),
                _ => null
            };

            // e.g. a requirement another mod registered with the game
            if (text == null)
                return Hints.ForPrecondition(c.Name) is { } hint ? hint.Text : I18n.Get("cond.other", new { raw = c.Raw });

            // conditions whose text above doesn't already reflect negation
            bool handlesNegation = c.Name.ToLowerInvariant() is "dayofweek" or "dayofmonth" or "season" or "sawevent" or "dating"
                or "spouse" or "roommate" or "hostmail" or "hostorlocalmail" or "localmail" or "festivalday" or "upcomingfestival"
                or "worldstate" or "activedialogueevent" or "skill" or "communitycenterorwarehousedone"
                || (c.Is("GameStateQuery") && !string.Join(" ", c.Args).Contains(','));
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

            // the flag's event was seen, but a different answer or branch was taken
            if (source.EventKey != null && index.Flags.IsMissedChoice(flag, Game1.player) && index.FindByKey(source.EventKey) is { } chosen)
                return I18n.Get($"{prefix}.missed-choice", new { @event = index.DescribeEventShort(chosen.Id) });

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
        /// How to open up the way to an event's area, from a hint in <c>assets/hints.json</c> if there is one, e.g.
        /// "Complete Marlon's special order 'Marlon's Boat'... (from Stardew Valley Expanded data)".
        /// </summary>
        /// <param name="forStep">Use the short lower-case form for "Not yet: ..." lines.</param>
        public static string DescribeReach(EventInfo evt, bool forStep)
        {
            if (AreaAccess.GetHint(evt.LocationName) is not { } hint)
                return I18n.Get(forStep ? "step.reach" : "reach.unknown", new { place = EventNarrator.WithArticle(evt.LocationDisplayName) });

            if (forStep)
                return char.ToLowerInvariant(hint.Text[0]) + hint.Text[1..];
            return hint.Source != null ? I18n.Get("flag.hint-source", new { hint = hint.Text, source = hint.Source }) : hint.Text;
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
                string? Player(int i) => t.Length > i && t[i].Equals("Any", StringComparison.OrdinalIgnoreCase) ? I18n.Get("gsq.anyone") : null;
                string Name(string npc) => npc.Equals("Any", StringComparison.OrdinalIgnoreCase) ? I18n.Get("someone") : EventIndex.GetNpcDisplayName(npc);
                string? clause = t[0].ToUpperInvariant() switch
                {
                    // ANY "query" "query": any one of them
                    "ANY" when !negated => t.Skip(1).Select(q => DescribeCondition(q, index)).OfType<string>().Distinct().ToList() is { Count: > 0 } any
                        ? JoinAlternatives(any)
                        : null,
                    "PLAYER_HAS_ITEM" when t.Length >= 3 => I18n.Get("gsq.has-item" + neg, new { item = ItemName(t[2]) }),
                    "PLAYER_FRIENDSHIP_POINTS" when !negated && t.Length >= 4 && int.TryParse(t[3], out int points) =>
                        points % NPC.friendshipPointsPerHeartLevel == 0
                            ? Hearts("gsq.hearts", points / NPC.friendshipPointsPerHeartLevel, Name(t[2]))
                            : I18n.Get("gsq.points", new { points, name = Name(t[2]) }),
                    "PLAYER_HEARTS" when !negated && t.Length >= 4 && int.TryParse(t[3], out int hearts) => Hearts("gsq.hearts", hearts, Name(t[2])),
                    "PLAYER_HAS_SEEN_EVENT" when t.Length >= 3 && index.FindById(t[2]) != null =>
                        I18n.Get("gsq.seen-event" + neg + (Player(1) != null ? ".anyone" : ""), new { @event = index.DescribeEventShort(t[2]) }),
                    "PLAYER_HAS_MAIL" when !negated && t.Length >= 3 => ExplainFlag(t[2], index, forStep: true, depth: 1) is { } mail
                        ? I18n.Get("gsq.via", new { step = mail })
                        : I18n.Get("gsq.flag", new { flag = HumanizeFlag(t[2]) }),
                    "PLAYER_HAS_MAIL" when t.Length >= 3 => I18n.Get("gsq.flag.not", new { flag = HumanizeFlag(t[2]) }),
                    "PLAYER_HAS_MET" when t.Length >= 3 => I18n.Get("gsq.met" + neg, new { name = Name(t[2]) }),

                    // secret notes from 1000 up are Ginger Island's journal scraps
                    "PLAYER_HAS_SECRET_NOTE" when !negated && t.Length >= 3 && int.TryParse(t[2], out int note) => note >= 1000
                        ? I18n.Get("gsq.journal-scrap", new { number = note - 1000 })
                        : I18n.Get("gsq.secret-note", new { number = note }),
                    "PLAYER_NPC_RELATIONSHIP" when t.Length >= 4 => I18n.Get("gsq.relationship" + neg, new
                    {
                        types = JoinList(t.Skip(3).Select(type => I18n.GetOr($"relationship.{type.ToLowerInvariant()}", type.ToLowerInvariant())), I18n.Get("join.or")),
                        name = negated && t[2].Equals("Any", StringComparison.OrdinalIgnoreCase) ? I18n.Get("gsq.anyone") : Name(t[2])
                    }),
                    "PLAYER_STAT" when !negated && t.Length >= 4 => I18n.Get("gsq.stat", new { stat = HumanizeFlag(t[2]).ToLowerInvariant(), count = t[3] }),
                    "PLAYER_VISITED_LOCATION" when t.Length >= 3 => I18n.Get("gsq.visited" + neg, new
                    {
                        place = JoinList(t.Skip(2).Select(l => EventNarrator.WithArticle(EventIndex.GetLocationDisplayName(l))), I18n.Get("join.or"))
                    }),
                    "PLAYER_SHIPPED_BASIC_ITEM" when !negated && t.Length >= 3 => I18n.Get("gsq.shipped", new { item = ItemName(t[2]), count = t.Length >= 4 ? t[3] : "1" }),
                    "BUILDINGS_CONSTRUCTED" when !negated && t.Length >= 3 => I18n.Get("gsq.built", new { building = BuildingName(t[2]) }),
                    "IS_PASSIVE_FESTIVAL_TODAY" when t.Length >= 2 => I18n.Get("gsq.passive-festival" + neg, new { festival = PassiveFestivalName(t[1]) }),
                    "SEASON" when t.Length >= 2 => I18n.Get("gsq.season" + neg, new { seasons = JoinList(t.Skip(1).Select(SeasonName), I18n.Get("join.or")) }),
                    "SEASON_DAY" when t.Length >= 3 => I18n.Get("gsq.season-day" + neg, new
                    {
                        dates = JoinList(Enumerable.Range(0, (t.Length - 1) / 2).Select(i => $"{SeasonName(t[1 + i * 2])} {t[2 + i * 2]}"), I18n.Get("join.or"))
                    }),
                    "DAY_OF_MONTH" when t.Length >= 2 => t[1].ToLowerInvariant() switch
                    {
                        "even" => I18n.Get("gsq.even-day" + neg),
                        "odd" => I18n.Get("gsq.odd-day" + neg),
                        _ => I18n.Get("gsq.day-of-month" + neg, new { days = JoinList(t.Skip(1), I18n.Get("join.or")) })
                    },
                    "WEATHER" when t.Length >= 3 => I18n.Get("gsq.weather" + neg, new { weather = JoinList(t.Skip(2).Select(w => WeatherName(w).ToLowerInvariant()), I18n.Get("join.or")) }),
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

        /// <summary>
        /// Alternatives as one phrase, without repeating their shared start: "complete the special order 'A', 'B' or
        /// 'C'" rather than "complete the special order 'A' or complete the special order 'B' or ...".
        /// </summary>
        private static string JoinAlternatives(List<string> options)
        {
            string or = I18n.Get("join.or");
            int quote = options[0].IndexOf('\'');
            if (options.Count > 1 && quote > 0)
            {
                string shared = options[0][..quote];
                if (options.All(o => o.StartsWith(shared, StringComparison.Ordinal) && o.Length > shared.Length && o[shared.Length] == '\''))
                    return shared + JoinList(options.Select(o => o[shared.Length..]), or);
            }
            return JoinList(options, or);
        }

        /// <summary>"have 1 heart" or "have 4 hearts".</summary>
        private static string Hearts(string key, int hearts, string name) =>
            I18n.Get(hearts == 1 ? key + ".1" : key, new { hearts, name });

        /// <summary>A building's display name from its type ID, e.g. a mod's "Broom Closet".</summary>
        private static string BuildingName(string type)
        {
            try
            {
                if (Game1.buildingData.TryGetValue(type, out var data) && TokenParser.ParseText(data.Name) is { Length: > 0 } name)
                    return name;
            }
            catch (Exception)
            {
                // fall back to the ID
            }
            return HumanizeFlag(type[(type.LastIndexOf('.') + 1)..]);
        }

        /// <summary>A passive festival's display name, e.g. "SquidFest" -> "SquidFest" as the game names it.</summary>
        private static string PassiveFestivalName(string id)
        {
            try
            {
                if (DataLoader.PassiveFestivals(Game1.content).TryGetValue(id, out var data) && TokenParser.ParseText(data.DisplayName) is { Length: > 0 } name)
                    return name;
            }
            catch (Exception)
            {
                // fall back to the ID
            }
            return HumanizeFlag(id);
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

        /// <summary>An NPC's stay at the event's location today, e.g. "Alex should be at the Beach about 1:10 pm-5:00 pm".</summary>
        public static string DescribeNpcStay(Precondition c, EventInfo evt, EventEvaluation eval, EventIndex index)
        {
            if (eval.NpcStay is not { } stay)
                return Describe(c, index, evt);

            var tokens = new
            {
                name = c.Args.Length > 0 ? EventIndex.GetNpcDisplayName(c.Args[0]) : I18n.Get("someone"),
                place = EventNarrator.WithArticle(evt.LocationDisplayName),
                range = I18n.Get("time.range", new { start = Time(stay.Start), end = Time(stay.End) })
            };
            return I18n.Get(stay.End <= Game1.timeOfDay ? "cond.npc-stay.left" : "cond.npc-stay", tokens);
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
                ? Hearts("cond.hearts", points / NPC.friendshipPointsPerHeartLevel, name)
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

        /// <summary>A note naming the arguments after the first, which the game ignores for weather and seasons; empty if there are none.</summary>
        private static string Ignored(string[] args, Func<string, string> name) =>
            args.Length > 1 ? I18n.Get("cond.ignored", new { rest = string.Join(I18n.Get("join.and"), args.Skip(1).Select(name)) }) : "";

        public static string SeasonName(string raw) => I18n.GetOr($"season.{raw.ToLowerInvariant()}", Capitalize(raw));

        /// <summary>A day of the week, short ("Mon"); also takes full names ("Friday"), which some mods use.</summary>
        public static string DayName(string raw) => I18n.GetOr($"day.{DayKey(raw)}", Capitalize(raw));

        /// <summary>A day of the week in full ("Monday"), for sentences like "Wait for a Monday".</summary>
        public static string LongDayName(string raw) => I18n.GetOr($"day.long.{DayKey(raw)}", DayName(raw));

        private static string DayKey(string raw) => raw.Length > 3 ? raw[..3].ToLowerInvariant() : raw.ToLowerInvariant();

        /// <summary>"On Mon/Thu", or for a long list the days it leaves out: "Not on Mon/Wed/Thu/Fri/Sat/Sun" reads better as "On Tue".</summary>
        private static string DescribeDays(string[] days, bool negated)
        {
            var week = new[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };
            var listed = days.Select(DayKey).Where(week.Contains).Distinct().ToList();
            var others = week.Except(listed).ToList();
            if (listed.Count > 0 && others.Count > 0 && others.Count < listed.Count)
                (listed, negated) = (others, !negated);
            var ordered = week.Where(listed.Contains).Select(DayName);
            return I18n.Get(negated ? "cond.day-of-week.not" : "cond.day-of-week", new { days = string.Join("/", ordered) });
        }

        /// <summary>A list as words: "A", "A or B", "A, B or C".</summary>
        public static string JoinList(IEnumerable<string> items, string conjunction)
        {
            var list = items.ToList();
            return list.Count <= 2 ? string.Join(conjunction, list) : string.Join(", ", list.Take(list.Count - 1)) + conjunction + list[^1];
        }

        /// <summary>Whether an NPC with this internal name exists in the game data (including other mods').</summary>
        public static bool IsCharacter(string name) => Game1.characterData.Keys.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>A skill's name as the game shows it ("Fishing"), from its internal name.</summary>
        private static string SkillName(string raw) => Capitalize(raw.ToLowerInvariant());

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
