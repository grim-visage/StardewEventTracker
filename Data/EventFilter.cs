using System;
using System.Collections.Generic;
using System.Linq;

namespace NpcEventTracker.Data
{
    /// <summary>The menu's search text and filter toggles, shared by all tabs for the rest of the session.</summary>
    internal static class EventFilter
    {
        public static string SearchText { get; set; } = "";

        public static bool AvailableNow { get; set; }
        public static bool AvailableToday { get; set; }
        public static bool WaitingOnDay { get; set; }
        public static bool ShowLocked { get; set; }

        /// <summary>Whether the Completed tab shows story events instead of heart events.</summary>
        public static bool CompletedShowsStory { get; set; }

        public static bool HasSearch => SearchText.Trim().Length > 0;

        public static bool HasStatusFilter => AvailableNow || AvailableToday || WaitingOnDay;

        /// <summary>Whether anything narrows the default view, which also auto-expands matching groups.</summary>
        public static bool IsActive => HasSearch || HasStatusFilter || ShowLocked;

        public static bool MatchesStatus(EventStatus status)
        {
            if (status == EventStatus.Locked)
                return ShowLocked;
            if (!HasStatusFilter)
                return true;

            return (AvailableNow && status == EventStatus.AvailableNow)
                || (AvailableToday && status is EventStatus.AvailableNow or EventStatus.LaterToday)
                || (WaitingOnDay && status is EventStatus.WrongDay or EventStatus.GreenRain or EventStatus.FestivalHere);
        }

        public static bool MatchesText(string? text) =>
            !HasSearch || (text != null && text.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Whether the search matches anything about the event: NPCs, location, ID, title or requirements.</summary>
        public static bool MatchesSearch(EventInfo evt, EventIndex index)
        {
            if (!HasSearch)
                return true;

            IEnumerable<string> haystack = new[] { evt.Id, evt.Title, evt.LocationDisplayName, evt.LocationName }
                .Concat(evt.Actors.Select(EventIndex.GetNpcDisplayName))
                .Concat(evt.Conditions.Select(c => PreconditionFormatter.Describe(c, index)));
            if (evt.IsHeartEvent)
                haystack = haystack.Append(EventIndex.GetNpcDisplayName(evt.Owner));

            return haystack.Any(MatchesText);
        }
    }
}
