using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>Which art to offer: single pictures, or folders of pictures for stack, random and meal art.</summary>
    public enum ArtKind
    {
        Picture,
        Folder,
    }

    /// <summary>
    /// Every running mod's art, read once from the mods' loaded textures, and the art that fits an item: single pictures
    /// for single art, folders for stack-count and random art.
    /// </summary>
    public static class ArtCatalog
    {
        private const string TexturesFolder = "Textures/";
        private static readonly string[] MaskEndings = { "_m", "_northm", "_eastm", "_southm", "_westm" };
        // Single art can use a four-direction picture's south view, as the game's own icons do.
        private static readonly string[] SideViewEndings = { "_north", "_east", "_west" };
        private static readonly string[] DirectionEndings = { "_north", "_east", "_south", "_west" };

        private static List<ModArt> mods;

        /// <summary>
        /// False when the option's art doesn't suit the item. Meal art comes in three equal groups (mixed, meat and
        /// vegetarian) of up to three stack sizes, so it takes 3, 6 or 9 pictures. Graphic_MealVariants throws for fewer
        /// than 3 or more than 11, and groups other counts oddly.
        /// </summary>
        public static bool Fits(ThingDef item, ArtOption option)
        {
            if (!option.IsFolder || !typeof(Graphic_MealVariants).IsAssignableFrom(item.graphicData.graphicClass))
            {
                return true;
            }
            return option.PictureCount == 3 || option.PictureCount == 6 || option.PictureCount == 9;
        }

        /// <summary>
        /// Art at the item's own path in each mod that has it, in load order, the base game first. Pictures for an item
        /// whose art is a folder are the pictures in that folder.
        /// </summary>
        public static List<ArtOption> ForItem(ThingDef item, ArtKind kind)
        {
            string path = item.graphicData.texPath;
            bool itemUsesFolder = PickRules.UsesFolder(item);
            var options = new List<ArtOption>();
            foreach (ModArt mod in Mods)
            {
                if (kind == ArtKind.Picture && itemUsesFolder)
                {
                    options.AddRange(mod.PicturesIn(path));
                    continue;
                }
                ArtOption option = mod.Find(path, kind == ArtKind.Folder);
                if (option != null && Fits(item, option))
                {
                    options.Add(option);
                }
            }
            return options;
        }

        /// <summary>
        /// Every mod's art of the kind whose path or mod name contains the text, grouped by mod in load order. An empty text
        /// matches everything.
        /// </summary>
        public static List<ArtOption> Search(ThingDef item, string text, ArtKind kind)
        {
            var found = new List<ArtOption>();
            foreach (ModArt mod in Mods)
            {
                bool modMatches = text.NullOrEmpty() || Contains(mod.Pack.Name, text);
                foreach (ArtOption option in kind == ArtKind.Folder ? mod.Folders : mod.Pictures)
                {
                    if ((modMatches || Contains(option.Path, text)) && Fits(item, option))
                    {
                        found.Add(option);
                    }
                }
            }
            return found;
        }

        /// <summary>The option a pick names, or null when its mod isn't running or has no such art.</summary>
        public static ArtOption Find(string packageId, string path, bool folder)
        {
            ModArt mod = Mods.FirstOrDefault(art =>
                string.Equals(art.Pack.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase));
            return mod?.Find(path, folder);
        }

        /// <summary>A texture from one mod only. The base game's art is in the game's Resources.</summary>
        public static Texture2D TextureFrom(ModContentPack mod, string path)
        {
            if (mod.IsCoreMod)
            {
                return Resources.Load<Texture2D>(TexturesFolder + path);
            }
            return mod.GetContentHolder<Texture2D>().Get(path);
        }

        private static List<ModArt> Mods => mods ??= ReadAllMods();

        private static List<ModArt> ReadAllMods()
        {
            Stopwatch timer = Stopwatch.StartNew();
            var all = new List<ModArt>();
            foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
            {
                try
                {
                    all.Add(new ModArt(pack, TexturePaths(pack)));
                }
                catch (Exception e)
                {
                    Log.Warning($"[Look Picker] Couldn't read the art of {pack.Name}, so it isn't offered: {e}");
                }
            }
            if (Prefs.DevMode)
            {
                Log.Message($"[Look Picker] Read {all.Sum(mod => mod.Pictures.Count)} pictures and " +
                    $"{all.Sum(mod => mod.Folders.Count)} folders from {all.Count} mods in {timer.ElapsedMilliseconds} ms.");
            }
            return all;
        }

        private static IEnumerable<string> TexturePaths(ModContentPack pack)
        {
            if (!pack.IsCoreMod)
            {
                return pack.GetContentHolder<Texture2D>().contentList.Keys.ToList();
            }
            // The base game's art is in the game's Resources, which only Waylith can list.
            string[] extensions = Waylith.Resources.LoadAllExtensionsFor(typeof(Waylith.Rendering.Texture2D));
            return Waylith.Resources.EnumerateResourcePaths("Textures", extensions)
                .Where(path => path.StartsWith(TexturesFolder, StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Substring(TexturesFolder.Length))
                .ToList();
        }

        private static bool Contains(string text, string part)
        {
            return text != null && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EndsWithAny(string name, string[] endings)
        {
            return endings.Any(ending => name.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class ModArt
        {
            private readonly Dictionary<string, ArtOption> picturesByPath;
            private readonly Dictionary<string, ArtOption> foldersByPath;
            private readonly Dictionary<string, List<ArtOption>> picturesByFolder;

            public ModArt(ModContentPack pack, IEnumerable<string> texturePaths)
            {
                Pack = pack;
                List<string> paths = texturePaths
                    .Where(path => !EndsWithAny(path, MaskEndings))
                    // Graphic_Collection sorts a folder's pictures with the default string comparer.
                    .OrderBy(path => path, StringComparer.CurrentCulture)
                    .ToList();
                Pictures = paths
                    .Where(path => !EndsWithAny(path, SideViewEndings))
                    .Select(path => new ArtOption(pack, path, isFolder: false, picturePaths: new[] { path }))
                    .ToList();
                // Stack and random art read a folder's subfolders too, but rebuild their paths wrongly, so only folders
                // without subfolders are offered. Paths are sorted, as the game sorts a folder's pictures.
                List<IGrouping<string, string>> folders = paths
                    .Where(path => path.Contains('/'))
                    .GroupBy(path => path.Substring(0, path.LastIndexOf('/')))
                    .ToList();
                var hasSubfolders = new HashSet<string>();
                foreach (IGrouping<string, string> folder in folders)
                {
                    for (int slash = folder.Key.LastIndexOf('/'); slash > 0; slash = folder.Key.LastIndexOf('/', slash - 1))
                    {
                        hasSubfolders.Add(folder.Key.Substring(0, slash));
                    }
                }
                // A folder of four-direction pictures makes one picture per direction set, not one per file
                // (Graphic_Collection.Init), so it's four-direction art and isn't offered.
                Folders = folders
                    .Where(folder => !hasSubfolders.Contains(folder.Key))
                    .Where(folder => !folder.Any(path => EndsWithAny(path, DirectionEndings)))
                    .Select(folder => new ArtOption(pack, folder.Key, isFolder: true, picturePaths: folder))
                    .ToList();
                // The base game's Resources ignore case, so an item's path can differ in case from the file.
                picturesByPath = new Dictionary<string, ArtOption>(StringComparer.OrdinalIgnoreCase);
                foreach (ArtOption picture in Pictures)
                {
                    picturesByPath[picture.Path] = picture;
                }
                foldersByPath = new Dictionary<string, ArtOption>(StringComparer.OrdinalIgnoreCase);
                foreach (ArtOption folder in Folders)
                {
                    foldersByPath[folder.Path] = folder;
                }
                picturesByFolder = Pictures
                    .Where(picture => picture.Path.Contains('/'))
                    .GroupBy(picture => picture.Path.Substring(0, picture.Path.LastIndexOf('/')), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(folder => folder.Key, folder => folder.ToList(), StringComparer.OrdinalIgnoreCase);
            }

            public ModContentPack Pack { get; }

            public List<ArtOption> Pictures { get; }

            public List<ArtOption> Folders { get; }

            public List<ArtOption> PicturesIn(string folder)
            {
                if (folder == null || !picturesByFolder.TryGetValue(folder.TrimEnd('/'), out List<ArtOption> pictures))
                {
                    return new List<ArtOption>();
                }
                return pictures;
            }

            public ArtOption Find(string path, bool folder)
            {
                if (path == null)
                {
                    return null;
                }
                Dictionary<string, ArtOption> options = folder ? foldersByPath : picturesByPath;
                return options.TryGetValue(path.TrimEnd('/'), out ArtOption option) ? option : null;
            }
        }
    }
}
