using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace NpcEventTracker.Data
{
    /// <summary>A festival's location and hours on a given day.</summary>
    internal readonly record struct FestivalInfo(string LocationName, int Start, int End);

    /// <summary>Festival and next-day lookups that mirror the game's own event rules.</summary>
    internal static class CalendarInfo
    {
        private static readonly Dictionary<string, FestivalInfo?> FestivalCache = new();

        /// <summary>Forgets cached festival data, e.g. when content packs reload.</summary>
        public static void Clear() => FestivalCache.Clear();

        /// <summary>The festival held at a location on a date, if any. The game blocks that location's events during festival hours.</summary>
        public static FestivalInfo? GetFestivalAt(string locationName, SDate date)
        {
            FestivalInfo? festival = GetFestival(date);
            return festival?.LocationName == locationName ? festival : null;
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

        /// <summary>Whether a festival's hours cover the whole time an event could start (all day if it has no window).</summary>
        public static bool CoversWindow(FestivalInfo festival, TimeWindow? window)
        {
            int start = window?.Start ?? 600;
            int end = window?.End ?? 2600;
            return festival.Start <= start && festival.End >= end;
        }
    }
}
