using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// Which art to offer: single pictures, folders of pictures for stack, random and meal art, or sets of facings for
    /// four-facing art.
    /// </summary>
    public enum ArtKind
    {
        Picture,
        Folder,
        Facings,
    }

    /// <summary>
    /// Every running mod's art, read once from the mods' loaded textures, and the art that fits an item: single pictures
    /// for single art, folders for stack-count and random art, sets of facings for four-facing art.
    /// </summary>
    public static class ArtCatalog
    {
        private const string TexturesFolder = "Textures/";
        private static readonly string[] MaskEndings = { "_m", "_northm", "_eastm", "_southm", "_westm" };
        // Single art can use a four-direction picture's south view, as the game's own icons do.
        private static readonly string[] SideViewEndings = { "_north", "_east", "_west" };
        // In the order a set of facings lists its pictures: the front first, as the game's icons show it.
        private static readonly string[] DirectionEndings = { "_south", "_east", "_north", "_west" };
        private const int South = 0, East = 1, North = 2, West = 3;
        // A common word like steel would otherwise fill the row with thousands of pictures.
        private const int MaxLikelyMatches = 40;
        private const int LabelWeight = 3;
        private const int OtherWeight = 1;
        // Words found all over mods' art names, which say nothing about which item the art is for.
        private static readonly HashSet<string> CommonWords = new HashSet<string>
        {
            "item", "thing", "building", "base", "the", "and", "for", "with", "small", "large", "big", "tiny",
        };

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
        /// whose art is a folder are the pictures in that folder. Facings are the set at the item's path. When other mods'
        /// patches moved the item's art to another path, the art at its path before those patches follows.
        /// </summary>
        public static List<ArtOption> ForItem(ThingDef item, ArtKind kind)
        {
            string path = item.graphicData.texPath;
            List<ArtOption> options = AtPath(item, path, kind);
            string pathBeforePatches = OriginalArt.PathBeforePatches(item);
            if (pathBeforePatches != null && !string.Equals(pathBeforePatches, path, StringComparison.OrdinalIgnoreCase))
            {
                options.AddRange(AtPath(item, pathBeforePatches, kind).Where(option => !options.Contains(option)));
            }
            return options;
        }

        /// <summary>
        /// Art from every mod whose file or folder name shares a word with the item, best first, leaving out the given
        /// options (the item's own row). A word from the item's label counts more than one from its defName or art file
        /// names. At most MaxLikelyMatches.
        /// </summary>
        public static List<ArtOption> LikelyMatches(ThingDef item, ArtKind kind, ICollection<ArtOption> leaveOut)
        {
            Dictionary<string, int> itemWords = ItemWords(item);
            if (itemWords.Count == 0)
            {
                return new List<ArtOption>();
            }
            var matches = new List<(ArtOption option, int score)>();
            foreach (ModArt mod in Mods)
            {
                foreach (ArtOption option in mod.OfKind(kind))
                {
                    int score = 0;
                    foreach (string word in option.NameWords)
                    {
                        if (itemWords.TryGetValue(word, out int weight))
                        {
                            score += weight;
                        }
                    }
                    if (score > 0 && !leaveOut.Contains(option) && Fits(item, option))
                    {
                        matches.Add((option, score));
                    }
                }
            }
            // OrderByDescending keeps load order among equal matches.
            return matches
                .OrderByDescending(match => match.score)
                .Take(MaxLikelyMatches)
                .Select(match => match.option)
                .ToList();
        }

        /// <summary>
        /// The words in a name, lower case, for matching names: split where a character isn't a letter and where a
        /// lower-case letter meets a capital, a trailing "s" dropped from words over three letters, and short and common
        /// words left out.
        /// </summary>
        public static List<string> WordsIn(string name)
        {
            var words = new List<string>();
            if (name.NullOrEmpty())
            {
                return words;
            }
            int start = -1;
            for (int i = 0; i <= name.Length; i++)
            {
                bool letter = i < name.Length && char.IsLetter(name[i]);
                bool newWord = letter && start >= 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]);
                if (start >= 0 && (!letter || newWord))
                {
                    AddWord(words, name.Substring(start, i - start));
                    start = -1;
                }
                if (letter && start < 0)
                {
                    start = i;
                }
            }
            return words;
        }

        private static void AddWord(List<string> words, string word)
        {
            word = word.ToLowerInvariant();
            if (word.Length > 3 && word.EndsWith('s'))
            {
                word = word.Substring(0, word.Length - 1);
            }
            if (word.Length >= 3 && !CommonWords.Contains(word))
            {
                words.Add(word);
            }
        }

        // Each word with its weight. Words from the item's label outweigh words from its defName and art file names, so
        // for a lung other lung art comes before the heart jar that shares "body part" with BodyPart_Lung, and a mod's
        // defName prefix (VCE_) doesn't rank all of that mod's art.
        private static Dictionary<string, int> ItemWords(ThingDef item)
        {
            var weights = new Dictionary<string, int>();
            AddWeighted(weights, WordsIn(FileName(item.graphicData?.texPath)), OtherWeight);
            AddWeighted(weights, WordsIn(FileName(OriginalArt.PathBeforePatches(item))), OtherWeight);
            AddWeighted(weights, WordsIn(item.defName), OtherWeight);
            AddWeighted(weights, WordsIn(item.label), LabelWeight);
            return weights;
        }

        private static void AddWeighted(Dictionary<string, int> weights, List<string> words, int weight)
        {
            foreach (string word in words)
            {
                if (!weights.TryGetValue(word, out int existing) || existing < weight)
                {
                    weights[word] = weight;
                }
            }
        }

        private static string FileName(string path)
        {
            if (path == null)
            {
                return null;
            }
            string trimmed = path.TrimEnd('/');
            return trimmed.Substring(trimmed.LastIndexOf('/') + 1);
        }

        private static List<ArtOption> AtPath(ThingDef item, string path, ArtKind kind)
        {
            bool itemUsesFolder = PickRules.UsesFolder(item);
            var options = new List<ArtOption>();
            foreach (ModArt mod in Mods)
            {
                if (kind == ArtKind.Picture && itemUsesFolder)
                {
                    options.AddRange(mod.PicturesIn(path));
                    continue;
                }
                ArtOption option = mod.Find(path, kind);
                if (option != null && Fits(item, option))
                {
                    options.Add(option);
                }
            }
            return options;
        }

        /// <summary>
        /// Every mod's art of the kind whose path or mod name contains the text, grouped by mod. Art whose file name matches
        /// best comes first, and the mods are ordered by their best match. An empty text matches everything, in load order.
        /// </summary>
        public static List<ArtOption> Search(ThingDef item, string text, ArtKind kind)
        {
            text = text?.Trim() ?? "";
            var foundByMod = new List<List<RankedOption>>();
            foreach (ModArt mod in Mods)
            {
                bool modMatches = text.Length == 0 || Contains(mod.Pack.Name, text);
                var modFound = new List<RankedOption>();
                foreach (ArtOption option in mod.OfKind(kind))
                {
                    if ((modMatches || Contains(option.Path, text)) && Fits(item, option))
                    {
                        modFound.Add(new RankedOption(option, text.Length == 0 ? 0 : PathRank(option, text)));
                    }
                }
                if (modFound.Count > 0)
                {
                    // OrderBy is stable, so equal matches keep their path order, and equal mods their load order.
                    foundByMod.Add(modFound.OrderBy(found => found.rank).ToList());
                }
            }
            return foundByMod
                .OrderBy(modFound => modFound[0].rank)
                .SelectMany(modFound => modFound.Select(found => found.option))
                .ToList();
        }

        // Ranked by the file name, then a match only in the path's folders, then only in the mod's name.
        private static int PathRank(ArtOption option, string text)
        {
            int rank = SearchRank.Of(option.Name, text);
            if (rank != SearchRank.NotInName)
            {
                return rank;
            }
            return Contains(option.Path, text) ? SearchRank.NotInName : SearchRank.NotInName + 1;
        }

        private readonly struct RankedOption
        {
            public readonly ArtOption option;
            public readonly int rank;

            public RankedOption(ArtOption option, int rank)
            {
                this.option = option;
                this.rank = rank;
            }
        }

        /// <summary>The option a pick names, or null when its mod isn't running or has no such art.</summary>
        public static ArtOption Find(string packageId, string path, ArtKind kind)
        {
            ModArt mod = Mods.FirstOrDefault(art =>
                string.Equals(art.Pack.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase));
            return mod?.Find(path, kind);
        }

        /// <summary>The kind of art a pick on the item uses: facings, a folder, or a single picture.</summary>
        public static ArtKind KindFor(ThingDef item, bool singlePicture)
        {
            if (PickRules.UsesFacings(item))
            {
                return ArtKind.Facings;
            }
            return PickRules.UsesFolder(item) && !singlePicture ? ArtKind.Folder : ArtKind.Picture;
        }

        /// <summary>The mod's own picture at the path, even where another mod replaces it, or null if it can't be read.</summary>
        public static Texture2D TextureFrom(ModContentPack mod, string path)
        {
            return ModOwnTextures.From(mod, path, out _);
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
                    Log.Warning($"[Age Picker] Couldn't read the art of {pack.Name}, so it isn't offered: {e}");
                }
            }
            if (Prefs.DevMode)
            {
                Log.Message($"[Age Picker] Read {all.Sum(mod => mod.Pictures.Count)} pictures, " +
                    $"{all.Sum(mod => mod.Folders.Count)} folders and {all.Sum(mod => mod.FacingSets.Count)} sets of facings " +
                    $"from {all.Count} mods in {timer.ElapsedMilliseconds} ms.");
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

        /// <summary>
        /// A set's four pictures, south, east, north and west, each missing one filled as Graphic_Multi.Init fills it: north
        /// from south, east or west; south from north; east from west, then north; west from east.
        /// </summary>
        private static List<string> FacingPaths(List<string> setPaths)
        {
            var facings = new string[4];
            for (int facing = South; facing <= West; facing++)
            {
                facings[facing] = setPaths.FirstOrDefault(path =>
                    path.EndsWith(DirectionEndings[facing], StringComparison.OrdinalIgnoreCase));
            }
            facings[North] = facings[North] ?? facings[South] ?? facings[East] ?? facings[West];
            facings[South] = facings[South] ?? facings[North];
            facings[East] = facings[East] ?? facings[West] ?? facings[North];
            facings[West] = facings[West] ?? facings[East];
            return facings.ToList();
        }

        private sealed class ModArt
        {
            private readonly Dictionary<string, ArtOption> picturesByPath;
            private readonly Dictionary<string, ArtOption> foldersByPath;
            private readonly Dictionary<string, ArtOption> facingSetsByPath;
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
                    .Select(path => new ArtOption(pack, path, ArtKind.Picture, new[] { path }))
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
                    .Select(folder => new ArtOption(pack, folder.Key, ArtKind.Folder, folder))
                    .ToList();
                FacingSets = paths
                    .Where(path => EndsWithAny(path, DirectionEndings))
                    .GroupBy(path => path.Substring(0, path.LastIndexOf('_')), StringComparer.OrdinalIgnoreCase)
                    .Select(set => new ArtOption(pack, set.Key, ArtKind.Facings, FacingPaths(set.ToList())))
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
                facingSetsByPath = new Dictionary<string, ArtOption>(StringComparer.OrdinalIgnoreCase);
                foreach (ArtOption set in FacingSets)
                {
                    facingSetsByPath[set.Path] = set;
                }
                picturesByFolder = Pictures
                    .Where(picture => picture.Path.Contains('/'))
                    .GroupBy(picture => picture.Path.Substring(0, picture.Path.LastIndexOf('/')), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(folder => folder.Key, folder => folder.ToList(), StringComparer.OrdinalIgnoreCase);
            }

            public ModContentPack Pack { get; }

            public List<ArtOption> Pictures { get; }

            public List<ArtOption> Folders { get; }

            public List<ArtOption> FacingSets { get; }

            public List<ArtOption> OfKind(ArtKind kind)
            {
                return kind == ArtKind.Folder ? Folders : kind == ArtKind.Facings ? FacingSets : Pictures;
            }

            public List<ArtOption> PicturesIn(string folder)
            {
                if (folder == null || !picturesByFolder.TryGetValue(folder.TrimEnd('/'), out List<ArtOption> pictures))
                {
                    return new List<ArtOption>();
                }
                return pictures;
            }

            public ArtOption Find(string path, ArtKind kind)
            {
                if (path == null)
                {
                    return null;
                }
                Dictionary<string, ArtOption> options = kind == ArtKind.Folder ? foldersByPath
                    : kind == ArtKind.Facings ? facingSetsByPath
                    : picturesByPath;
                return options.TryGetValue(path.TrimEnd('/'), out ArtOption option) ? option : null;
            }
        }
    }
}
