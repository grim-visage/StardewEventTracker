using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI.Utilities;

namespace StardewEventTracker.Data
{
    /// <summary>The menu's search text and filter toggles, shared by all tabs for the rest of the session.</summary>
    internal static class EventFilter
    {
        private sealed class State
        {
            public string SearchText = "";
            public bool AvailableNow, AvailableToday, WaitingOnDay, ShowLocked, CompletedShowsStory;
        }

        // each split-screen player has their own search and filters
        private static readonly PerScreen<State> Current = new(() => new State());

        public static string SearchText { get => Current.Value.SearchText; set => Current.Value.SearchText = value; }

        public static bool AvailableNow { get => Current.Value.AvailableNow; set => Current.Value.AvailableNow = value; }
        public static bool AvailableToday { get => Current.Value.AvailableToday; set => Current.Value.AvailableToday = value; }
        public static bool WaitingOnDay { get => Current.Value.WaitingOnDay; set => Current.Value.WaitingOnDay = value; }
        public static bool ShowLocked { get => Current.Value.ShowLocked; set => Current.Value.ShowLocked = value; }

        /// <summary>Whether the Completed tab shows story events instead of heart events.</summary>
        public static bool CompletedShowsStory { get => Current.Value.CompletedShowsStory; set => Current.Value.CompletedShowsStory = value; }

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
                || (WaitingOnDay && status is EventStatus.WrongDay or EventStatus.GreenRain or EventStatus.FestivalHere or EventStatus.MissedToday);
        }

        public static bool MatchesText(string? text) =>
            !HasSearch || (text != null && text.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Whether the search matches anything about the event: NPCs, location, ID, title or requirements.</summary>
        /// <param name="detailsHidden">Spoiler-free mode hides this event's details, so only its title and NPC can match.</param>
        public static bool MatchesSearch(EventInfo evt, EventIndex index, bool detailsHidden = false)
        {
            if (!HasSearch)
                return true;
            if (detailsHidden)
                return MatchesText(evt.Title) || (evt.IsHeartEvent && MatchesText(EventIndex.GetNpcDisplayName(evt.Owner)));

            IEnumerable<string> haystack = new[] { evt.Id, evt.Title, evt.LocationDisplayName, evt.LocationName }
                .Concat(evt.Actors.Select(EventIndex.GetNpcDisplayName))
                .Concat(evt.Conditions.Select(c => PreconditionFormatter.Describe(c, index)));
            if (evt.IsHeartEvent)
                haystack = haystack.Append(EventIndex.GetNpcDisplayName(evt.Owner));

            return haystack.Any(MatchesText);
        }
    }
}
