using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;

namespace StardewEventTracker.Data
{
    /// <summary>An event a Content Patcher pack adds only under some conditions, as written in the pack.</summary>
    /// <param name="Id">The event ID.</param>
    /// <param name="Location">The location whose events the patch edits.</param>
    /// <param name="Key">The event's key: its ID and preconditions.</param>
    /// <param name="ModName">The content pack's name.</param>
    /// <param name="When">The patch's conditions (Content Patcher tokens).</param>
    internal sealed record PackEvent(string Id, string Location, string Key, string ModName, IReadOnlyDictionary<string, string> When);

    /// <summary>
    /// Reads the loaded Content Patcher packs for events they add to location event data, so an event that's needed
    /// but not in the game yet (its patch's conditions aren't met) can be explained: what makes the mod add it.
    /// The files don't change while the game runs, so they're read once, in the background.
    /// </summary>
    internal static class ContentPackEvents
    {
        /// <summary>A patch target for a location's events: "Data/Events/Farm".</summary>
        private static readonly Regex EventsTarget = new(@"^Data[/\\]Events[/\\]([^/\\{}]+)$", RegexOptions.IgnoreCase);

        private static volatile Dictionary<string, List<PackEvent>> byId = new();

        /// <summary>The conditional versions of an event, from every pack that adds it; empty if none (or the scan hasn't finished).</summary>
        public static IReadOnlyList<PackEvent> Get(string id) =>
            byId.TryGetValue(id, out List<PackEvent>? events) ? events : Array.Empty<PackEvent>();

        /// <summary>Starts reading the loaded content packs in the background.</summary>
        public static void ScanInBackground(IModHelper helper, IMonitor monitor)
        {
            Task.Run(() =>
            {
                try
                {
                    var found = new Dictionary<string, List<PackEvent>>();
                    int packs = 0;
                    foreach ((string dir, string name) in FindContentPatcherPacks(helper))
                    {
                        packs++;
                        foreach (string file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
                        {
                            if (Path.GetFileName(file).Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || file.Contains($"{Path.DirectorySeparatorChar}i18n{Path.DirectorySeparatorChar}"))
                                continue;
                            ReadPatches(file, name, found);
                        }
                    }
                    byId = found;
                    monitor.Log($"Read {found.Count} conditionally added events from {packs} Content Patcher packs.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    monitor.Log($"Couldn't read Content Patcher packs to explain events they add later: {ex.Message}", LogLevel.Trace);
                }
            });
        }

        /// <summary>The folder and name of every loaded content pack for Content Patcher (folders starting with '.' are disabled, as in SMAPI).</summary>
        private static IEnumerable<(string Dir, string Name)> FindContentPatcherPacks(IModHelper helper)
        {
            var pending = new Stack<string>();
            pending.Push(ModsFolder(helper));
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                string manifestPath = Path.Combine(dir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    if (TryReadManifest(manifestPath, out string? id, out string? name, out string? packFor)
                        && packFor?.Equals("Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase) == true
                        && id != null && helper.ModRegistry.IsLoaded(id))
                        yield return (dir, name ?? id);
                    continue;
                }

                foreach (string sub in Directory.EnumerateDirectories(dir))
                {
                    if (!Path.GetFileName(sub).StartsWith('.'))
                        pending.Push(sub);
                }
            }
        }

        /// <summary>The Mods folder: the top folder under the game's that holds this mod, or this mod's parent folder if it's elsewhere.</summary>
        private static string ModsFolder(IModHelper helper)
        {
            string game = Path.GetFullPath(Constants.GamePath).TrimEnd(Path.DirectorySeparatorChar);
            string dir = Path.GetFullPath(helper.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar);
            string? parent = Path.GetDirectoryName(dir);
            while (parent != null && !parent.Equals(game, StringComparison.OrdinalIgnoreCase))
            {
                dir = parent;
                parent = Path.GetDirectoryName(dir);
            }
            return parent != null ? dir : Path.GetDirectoryName(Path.GetFullPath(helper.DirectoryPath))!;
        }

        private static bool TryReadManifest(string path, out string? id, out string? name, out string? packFor)
        {
            id = name = packFor = null;
            if (Parse(path) is not JObject manifest)
                return false;
            id = manifest.Value<string>("UniqueID");
            name = manifest.Value<string>("Name");
            packFor = (manifest["ContentPackFor"] as JObject)?.Value<string>("UniqueID");
            return true;
        }

        /// <summary>Records the location events each EditData patch in the file adds, with the patch's conditions.</summary>
        private static void ReadPatches(string file, string modName, Dictionary<string, List<PackEvent>> found)
        {
            if (Parse(file) is not JObject { } root || root["Changes"] is not JArray changes)
                return;

            foreach (JObject patch in changes.OfType<JObject>())
            {
                if (!string.Equals(patch.Value<string>("Action"), "EditData", StringComparison.OrdinalIgnoreCase) || patch["Entries"] is not JObject entries)
                    continue;

                var locations = (patch.Value<string>("Target") ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => EventsTarget.Match(t))
                    .Where(m => m.Success)
                    .Select(m => m.Groups[1].Value)
                    .ToList();
                if (locations.Count == 0)
                    continue;

                var when = (patch["When"] as JObject)?.Properties()
                    .ToDictionary(p => p.Name, p => p.Value.Type == JTokenType.Boolean ? p.Value.ToString().ToLowerInvariant() : p.Value.ToString(), StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>();

                foreach (JProperty entry in entries.Properties())
                {
                    // null entries remove an event rather than add one
                    if (entry.Value.Type == JTokenType.Null || entry.Name.Contains("{{"))
                        continue;
                    string id = entry.Name.Split('/')[0];
                    foreach (string location in locations)
                    {
                        if (!found.TryGetValue(id, out List<PackEvent>? list))
                            found[id] = list = new List<PackEvent>();
                        list.Add(new PackEvent(id, location, entry.Name, modName, when));
                    }
                }
            }
        }

        private static JToken? Parse(string path)
        {
            try
            {
                using var reader = new JsonTextReader(new StreamReader(path));
                return JToken.ReadFrom(reader, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore, DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Ignore });
            }
            catch (Exception)
            {
                // not JSON Content Patcher can read either, or not a patch file
                return null;
            }
        }
    }
}
