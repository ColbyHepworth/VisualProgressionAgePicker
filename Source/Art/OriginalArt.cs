using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// Where an item's art was before other mods' patches moved it, read from its own mod's Defs files on disk. A retexture
    /// that points an item at new files of its own (Van's organ jars) hides the item's original art from the item's row.
    /// </summary>
    public static class OriginalArt
    {
        private static readonly Dictionary<ModContentPack, ModDefs> modDefs = new Dictionary<ModContentPack, ModDefs>();

        /// <summary>The item's art path as its own mod's Defs write it, before any patch, or null when that can't be read.</summary>
        public static string PathBeforePatches(ThingDef item)
        {
            ModContentPack mod = item.modContentPack;
            if (mod == null)
            {
                return null;
            }
            DefEntry entry = DefsOf(mod).Find(item.defName, byDefName: true);
            // Parents can't loop: the game refuses cyclic inheritance, so this only guards against a broken file.
            for (int depth = 0; entry != null && depth < 50; depth++)
            {
                if (entry.TexPath != null)
                {
                    return entry.TexPath;
                }
                if (entry.ArtNotInherited || entry.ParentName == null)
                {
                    return null;
                }
                entry = FindParent(mod, entry.ParentName);
            }
            return null;
        }

        // The game can take a parent from any mod loaded before the item's. The item's own mod, then the base game and its
        // expansions, cover nearly all of them without reading every mod's files.
        private static DefEntry FindParent(ModContentPack itemMod, string parentName)
        {
            DefEntry parent = DefsOf(itemMod).Find(parentName, byDefName: false);
            if (parent != null)
            {
                return parent;
            }
            foreach (ModContentPack official in LoadedModManager.RunningModsListForReading.Where(mod => mod.IsOfficialMod))
            {
                parent = DefsOf(official).Find(parentName, byDefName: false);
                if (parent != null)
                {
                    return parent;
                }
            }
            return null;
        }

        private static ModDefs DefsOf(ModContentPack mod)
        {
            if (!modDefs.TryGetValue(mod, out ModDefs defs))
            {
                defs = ReadDefs(mod);
                modDefs[mod] = defs;
            }
            return defs;
        }

        private static ModDefs ReadDefs(ModContentPack mod)
        {
            Stopwatch timer = Stopwatch.StartNew();
            var defs = new ModDefs();
            List<string> files;
            try
            {
                files = DefFiles(mod).ToList();
            }
            catch (Exception e)
            {
                Log.Warning($"[Age Picker] Couldn't list the Defs of {mod.Name}, so its items' art from before other mods' " +
                    $"patches isn't offered: {e.Message}");
                return defs;
            }
            int unreadable = 0;
            string firstProblem = null;
            foreach (string file in files)
            {
                try
                {
                    AddThingDefs(file, defs);
                }
                catch (Exception e)
                {
                    unreadable++;
                    firstProblem ??= $"{file}: {e.Message}";
                }
            }
            if (unreadable > 0)
            {
                Log.Warning($"[Age Picker] Couldn't read {unreadable} of {mod.Name}'s Defs files, so items in them don't " +
                    $"offer their art from before other mods' patches. First: {firstProblem}");
            }
            if (Prefs.DevMode)
            {
                Log.Message($"[Age Picker] Read {mod.Name}'s Defs for art before patches in {timer.ElapsedMilliseconds} ms.");
            }
            return defs;
        }

        private static void AddThingDefs(string file, ModDefs defs)
        {
            var document = new XmlDocument();
            document.Load(file);
            if (document.DocumentElement == null)
            {
                return;
            }
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "ThingDef")
                {
                    defs.Add(element);
                }
            }
        }

        // The files the game reads as the mod's Defs: each load folder's Defs, the first file at a path winning
        // (DirectXmlLoader.XmlAssetsInModFolder). That loader isn't called, since mods that cache the loaded XML hook it.
        private static IEnumerable<string> DefFiles(ModContentPack mod)
        {
            var filesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in mod.foldersToLoadDescendingOrder)
            {
                var defsFolder = new DirectoryInfo(Path.Combine(folder, "Defs"));
                if (!defsFolder.Exists)
                {
                    continue;
                }
                foreach (FileInfo file in defsFolder.GetFiles("*.xml", SearchOption.AllDirectories))
                {
                    if (!file.Name.StartsWith("."))
                    {
                        filesByPath.TryAdd(file.FullName.Substring(folder.Length + 1), file.FullName);
                    }
                }
            }
            return filesByPath.Values;
        }

        private sealed class DefEntry
        {
            public string ParentName;
            public string TexPath;
            public bool ArtNotInherited;
        }

        // A mod's ThingDefs by defName and by Name, with only what finding their art needs.
        private sealed class ModDefs
        {
            private readonly Dictionary<string, DefEntry> byDefName = new Dictionary<string, DefEntry>();
            private readonly Dictionary<string, DefEntry> byName = new Dictionary<string, DefEntry>();

            public void Add(XmlElement def)
            {
                XmlNode graphicData = def["graphicData"];
                var entry = new DefEntry
                {
                    ParentName = NullIfEmpty(def.GetAttribute("ParentName")),
                    TexPath = NullIfEmpty(graphicData?["texPath"]?.InnerText.Trim()),
                    ArtNotInherited = graphicData is XmlElement element &&
                        string.Equals(element.GetAttribute("Inherit"), "false", StringComparison.OrdinalIgnoreCase),
                };
                string defName = def["defName"]?.InnerText.Trim();
                if (!defName.NullOrEmpty())
                {
                    byDefName[defName] = entry;
                }
                string name = def.GetAttribute("Name");
                if (!name.NullOrEmpty())
                {
                    byName[name] = entry;
                }
            }

            public DefEntry Find(string key, bool byDefName)
            {
                return (byDefName ? this.byDefName : byName).TryGetValue(key, out DefEntry entry) ? entry : null;
            }

            private static string NullIfEmpty(string text)
            {
                return text.NullOrEmpty() ? null : text;
            }
        }
    }
}
