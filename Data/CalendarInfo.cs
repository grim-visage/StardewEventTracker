using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData;
using StardewValley.TokenizableStrings;

namespace StardewEventTracker.Data
{
    /// <summary>A festival's location and hours on a given day.</summary>
    internal readonly record struct FestivalInfo(string LocationName, int Start, int End);

    /// <summary>Festival and next-day lookups that mirror the game's own event rules.</summary>
    internal static class CalendarInfo
    {
        private static readonly Dictionary<string, FestivalInfo?> FestivalCache = new();

        /// <summary>Forgets cached festival data, e.g. when content packs reload.</summary>
        public static void Clear() => FestivalCache.Clear();

        /// <summary>The festival held at a location on a date, if any. The game blocks that location from the start of the day until the festival ends.</summary>
        public static FestivalInfo? GetFestivalAt(string locationName, SDate date)
        {
            FestivalInfo? festival = GetFestival(date);
            return festival?.LocationName == locationName ? festival : null;
        }

        /// <summary>
        /// The passive festival (e.g. the Night Market) that replaces this location on the date: the game sends anyone
        /// going there to the festival's own location all day, so the location's events can't play. Null if none.
        /// </summary>
        public static string? PassiveFestivalReplacing(string locationName, SDate date)
        {
            try
            {
                foreach ((string id, PassiveFestivalData data) in DataLoader.PassiveFestivals(Game1.content))
                {
                    if (data.Season != date.Season || date.Day < data.StartDay || date.Day > data.EndDay || data.MapReplacements?.ContainsKey(locationName) != true)
                        continue;
                    if (!GameStateQuery.CheckConditions(data.Condition))
                        continue;
                    return TokenParser.ParseText(data.DisplayName) is { Length: > 0 } name ? name : id;
                }
            }
            catch (Exception)
            {
                // treat unreadable festival data as no festival
            }
            return null;
        }

        public static FestivalInfo? GetFestival(SDate date)
        {
            string key = $"{date.SeasonKey}{date.Day}";
            if (FestivalCache.TryGetValue(key, out FestivalInfo? cached))
                return cached;

            FestivalInfo? result = null;
            try
            {
                if (Event.tryToLoadFestivalData(key, out _, out _, out string locationName, out int start, out int end))
                    result = new FestivalInfo(locationName, start, end);
            }
            catch (Exception)
            {
                // treat unreadable festival data as no festival
            }
            return FestivalCache[key] = result;
        }

        /// <summary>Whether it'll be raining tomorrow in the location's weather context, per the forecast.</summary>
        public static string WeatherTomorrow(GameLocation location) =>
            location.GetWeather()?.WeatherForTomorrow ?? Game1.weatherForTomorrow ?? "Sun";

        private static bool IsRainy(string weather) => weather is "Rain" or "Storm" or "GreenRain";

        /// <summary>
        /// Whether an event's calendar requirements and day-wide blockers (Green Rain, a festival covering its whole
        /// time window) all allow it tomorrow. Returns null if a requirement can't be predicted.
        /// </summary>
        public static bool? WorksTomorrow(EventInfo evt, GameLocation location)
        {
            SDate tomorrow = SDate.Now().AddDays(1);
            string weather = WeatherTomorrow(location);

            if (weather == "GreenRain")
                return false;

            if (GetFestivalAt(evt.LocationName, tomorrow) is { } festival && CoversWindow(festival, evt.Window))
                return false;

            bool unknown = false;
            foreach (Precondition c in evt.Conditions.Where(c => c.Category == ConditionCategory.Calendar))
            {
                bool? result = c.Name.ToLowerInvariant() switch
                {
                    // the game only reads the first weather and season argument
                    "weather" => c.Args.FirstOrDefault() switch
                    {
                        null => null,
                        "rainy" => IsRainy(weather),
                        "sunny" => !IsRainy(weather),
                        string id => id == weather
                    },
                    "season" => c.Args.Length > 0 && Enum.TryParse(c.Args[0], ignoreCase: true, out Season season) ? season == tomorrow.Season : null,
                    "dayofweek" => c.Args.Any(a => WorldDate.TryGetDayOfWeekFor(a, out DayOfWeek day) && day == tomorrow.DayOfWeek),
                    "dayofmonth" => c.Args.Any(a => int.TryParse(a, out int day) && day == tomorrow.Day),
                    "festivalday" => Utility.isFestivalDay(tomorrow.Day, tomorrow.Season, location.GetLocationContextId()),
                    _ => null
                };

                if (result == null)
                    unknown = true;
                else if (result.Value == c.Negated)
                    return false;
            }

            return unknown ? null : true;
        }

        /// <summary>
        /// When the festival's location lets players in again (HHMM). The game blocks warps there from the start of the day
        /// while the festival is set up, and still sends arrivals to the festival at its end time, so it's 10 minutes after.
        /// </summary>
        public static int ReopensAfter(FestivalInfo festival) => Utility.ModifyTime(festival.End, 10);

        /// <summary>Whether a festival keeps players out for the whole time an event could start (all day if it has no window).</summary>
        public static bool CoversWindow(FestivalInfo festival, TimeWindow? window) =>
            festival.End >= (window?.End ?? 2600);
    }
}
