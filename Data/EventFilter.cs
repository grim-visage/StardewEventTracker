using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI.Utilities;

namespace StardewEventTracker.Data
{
    /// <summary>How the menu orders its groups (NPCs or locations).</summary>
    internal enum EventSort
    {
        /// <summary>Alphabetical (pinned NPCs first on the Hearts tab).</summary>
        Name,

        /// <summary>Groups with an event available now first, then later today, and so on.</summary>
        Ready,

        /// <summary>Most hearts first; NPCs you haven't met last.</summary>
        Hearts,

        /// <summary>Most events first: unseen ones, or seen ones on the Completed tab.</summary>
        Count,

        /// <summary>Completed tab: the share of each group's events seen, highest first.</summary>
        Progress
    }

    /// <summary>The menu's search text and filter toggles, shared by all tabs for the rest of the session.</summary>
    internal static class EventFilter
    {
        private sealed class State
        {
            public string SearchText = "";
            public bool AvailableNow, AvailableToday, WaitingOnDay, ShowLocked, CompletedShowsStory;
            public readonly Dictionary<string, EventSort> Sort = new();
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

        /// <summary>The sorts a tab offers, its default first.</summary>
        public static EventSort[] SortsFor(string tab) => tab switch
        {
            "story" => new[] { EventSort.Ready, EventSort.Name, EventSort.Count },
            "completed" => new[] { EventSort.Name, EventSort.Progress, EventSort.Count },
            _ => new[] { EventSort.Name, EventSort.Ready, EventSort.Hearts, EventSort.Count }
        };

        /// <summary>The sort chosen for a tab, or its default.</summary>
        public static EventSort GetSort(string tab) =>
            Current.Value.Sort.TryGetValue(tab, out EventSort sort) ? sort : SortsFor(tab)[0];

        public static void SetSort(string tab, EventSort sort) => Current.Value.Sort[tab] = sort;

        public static bool HasSearch => SearchText.Trim().Length > 0;

        public static bool HasStatusFilter => AvailableNow || AvailableToday || WaitingOnDay;

        /// <summary>Whether anything narrows the default view, which also auto-expands matching groups.</summary>
        public static bool IsActive => HasSearch || HasStatusFilter || ShowLocked;

        /// <summary>Whether matching groups open by themselves: for the filter buttons, but not a search, which lists matching groups closed.</summary>
        public static bool AutoExpands => HasStatusFilter || ShowLocked;

        /// <summary>Changes whenever the search or a filter does.</summary>
        public static string Signature => $"{SearchText.Trim().ToLowerInvariant()}|{AvailableNow}|{AvailableToday}|{WaitingOnDay}|{ShowLocked}";

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

            // worked out once per event, not again on every key typed
            return index.GetSearchText(evt).Any(MatchesText);
        }
    }
}
