using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
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

    /// <summary>A map tile a pack adds that marks an event ID seen when checked ("MessageOnce &lt;id&gt; &lt;message&gt;").</summary>
    /// <param name="Map">The map the patch edits, usually the location's name.</param>
    internal sealed record TileMarker(string Id, string Map, int X, int Y, IReadOnlyDictionary<string, string> When);

    /// <summary>
    /// Reads the loaded Content Patcher packs for events they add to location event data, so an event that's needed
    /// but not in the game yet (its patch's conditions aren't met) can be explained: what makes the mod add it.
    /// The files don't change while the game runs, so they're read once, in the background.
    /// </summary>
    internal static class ContentPackEvents
    {
        /// <summary>A patch target for a location's events: "Data/Events/Farm".</summary>
        private static readonly Regex EventsTarget = new(@"^Data[/\\]Events[/\\]([^/\\{}]+)$", RegexOptions.IgnoreCase);

        /// <summary>A patch target for a map: "Maps/Custom_Ridgeside_RidgesideVillage".</summary>
        private static readonly Regex MapTarget = new(@"^Maps[/\\]([^/\\{}]+)$", RegexOptions.IgnoreCase);

        /// <summary>Anything in a pack that marks an event ID seen: a trigger or dialogue action, an event command, a map tile action.</summary>
        private static readonly Regex SetsEventSeen = new(@"(?:MarkEventSeen\s+\S+|MessageOnce|\beventSeen)\s+([^\s""'/\\]+)", RegexOptions.IgnoreCase);

        /// <summary>Every file below a folder, skipping folders that can't be read (so one locked folder doesn't stop the scan).</summary>
        private static readonly EnumerationOptions AllAccessible = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

        private static volatile Dictionary<string, List<PackEvent>> byId = new();
        private static volatile Dictionary<string, TileMarker> tileMarkers = new();
        private static volatile HashSet<string> setSomewhere = new();

        private static volatile bool ready;

        /// <summary>Whether the packs have been read, so an event none of them adds really isn't added by any.</summary>
        /// <remarks>Volatile, and set after the results, so the main thread never sees it before them.</remarks>
        public static bool Ready => ready;

        /// <summary>The map tile that marks this event ID seen, if a pack adds one.</summary>
        public static TileMarker? GetTileMarker(string id) => tileMarkers.TryGetValue(id, out TileMarker? marker) ? marker : null;

        /// <summary>Whether anything in a pack marks this event ID seen (a trigger, dialogue, event command or map tile).</summary>
        public static bool IsSetSomewhere(string id) => setSomewhere.Contains(id);

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
                    var tiles = new Dictionary<string, TileMarker>();
                    var setters = new HashSet<string>();
                    int packs = 0;
                    // C# mods can mark events seen in code, so an ID they mention isn't ruled out
                    foreach (string dll in FindCodeMods(helper))
                        ReadCodeStrings(dll, setters);

                    foreach ((string dir, string name) in FindContentPatcherPacks(helper))
                    {
                        packs++;
                        foreach (string file in Directory.EnumerateFiles(dir, "*.json", AllAccessible))
                        {
                            if (Path.GetFileName(file).Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || file.Contains($"{Path.DirectorySeparatorChar}i18n{Path.DirectorySeparatorChar}"))
                                continue;
                            // one odd file (a token where a number goes) shouldn't stop the rest from being read
                            try
                            {
                                ReadPatches(file, name, found, tiles, setters);
                            }
                            catch (Exception ex)
                            {
                                monitor.Log($"Skipped {file} while reading Content Patcher packs: {ex.Message}", LogLevel.Trace);
                            }
                        }
                    }
                    byId = found;
                    tileMarkers = tiles;
                    setSomewhere = setters;
                    ready = true;
                    monitor.Log($"Read {found.Count} conditionally added events from {packs} Content Patcher packs.", LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    monitor.Log($"Couldn't read Content Patcher packs to explain events they add later: {ex.Message}", LogLevel.Trace);
                }
            });
        }

        /// <summary>The main DLL of every loaded C# mod (except this one).</summary>
        private static IEnumerable<string> FindCodeMods(IModHelper helper)
        {
            foreach (string manifestPath in Directory.EnumerateFiles(ModsFolder(helper), "manifest.json", AllAccessible))
            {
                if (manifestPath.Split(Path.DirectorySeparatorChar).Any(part => part.StartsWith('.')) || Parse(SafeRead(manifestPath)) is not JObject manifest)
                    continue;
                string? id = manifest.Value<string>("UniqueID");
                string? entryDll = manifest.Value<string>("EntryDll");
                if (id == null || entryDll == null || id == helper.ModRegistry.ModID || !helper.ModRegistry.IsLoaded(id))
                    continue;
                string dll = Path.Combine(Path.GetDirectoryName(manifestPath)!, entryDll);
                if (File.Exists(dll))
                    yield return dll;
            }
        }

        /// <summary>Adds the string literals in a DLL's code, read from its metadata without loading it.</summary>
        private static void ReadCodeStrings(string dll, HashSet<string> strings)
        {
            try
            {
                using var stream = File.OpenRead(dll);
                using var pe = new PEReader(stream);
                MetadataReader metadata = pe.GetMetadataReader();
                for (UserStringHandle handle = MetadataTokens.UserStringHandle(1); !handle.IsNil; handle = metadata.GetNextHandle(handle))
                {
                    string value = metadata.GetUserString(handle);
                    if (value.Length is > 0 and < 100)
                        strings.Add(value);
                }
            }
            catch (Exception)
            {
                // not a .NET assembly we can read
            }
        }

        private static string SafeRead(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception)
            {
                return "";
            }
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

                foreach (string sub in Directory.EnumerateDirectories(dir, "*", new EnumerationOptions { IgnoreInaccessible = true }))
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
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception)
            {
                return false;
            }
            if (Parse(text) is not JObject manifest)
                return false;
            id = manifest.Value<string>("UniqueID");
            name = manifest.Value<string>("Name");
            packFor = (manifest["ContentPackFor"] as JObject)?.Value<string>("UniqueID");
            return true;
        }

        /// <summary>Records the location events each EditData patch in the file adds, with the patch's conditions.</summary>
        private static void ReadPatches(string file, string modName, Dictionary<string, List<PackEvent>> found, Dictionary<string, TileMarker> tiles, HashSet<string> setters)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception)
            {
                return;
            }
            foreach (Match match in SetsEventSeen.Matches(text))
                setters.Add(match.Groups[1].Value);

            if (Parse(text) is not JObject { } root || root["Changes"] is not JArray changes)
                return;

            foreach (JObject patch in changes.OfType<JObject>())
            {
                if (string.Equals(patch.Value<string>("Action"), "EditMap", StringComparison.OrdinalIgnoreCase))
                {
                    ReadTileMarkers(patch, tiles);
                    continue;
                }
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

                IReadOnlyDictionary<string, string> when = ReadWhen(patch);

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

        /// <summary>Records map tiles the patch adds whose action marks an event seen when checked (MessageOnce).</summary>
        private static void ReadTileMarkers(JObject patch, Dictionary<string, TileMarker> tiles)
        {
            Match target = MapTarget.Match(patch.Value<string>("Target") ?? "");
            if (!target.Success || patch["MapTiles"] is not JArray mapTiles)
                return;

            IReadOnlyDictionary<string, string> when = ReadWhen(patch);
            foreach (JObject tile in mapTiles.OfType<JObject>())
            {
                string action = (tile["SetProperties"] as JObject)?.Value<string>("Action") ?? "";
                string[] words = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 2 || !words[0].Equals("MessageOnce", StringComparison.OrdinalIgnoreCase) || tile["Position"] is not JObject position)
                    continue;
                tiles.TryAdd(words[1], new TileMarker(words[1], target.Groups[1].Value, position.Value<int>("X"), position.Value<int>("Y"), when));
            }
        }

        private static IReadOnlyDictionary<string, string> ReadWhen(JObject patch) =>
            (patch["When"] as JObject)?.Properties()
                .ToDictionary(p => p.Name, p => p.Value.Type == JTokenType.Boolean ? p.Value.ToString().ToLowerInvariant() : p.Value.ToString(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>();

        private static JToken? Parse(string text)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(text));
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
