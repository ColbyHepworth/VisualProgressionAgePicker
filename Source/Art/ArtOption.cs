using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>One picture, one folder of pictures or one set of facings a mod has that a pick can use.</summary>
    public sealed class ArtOption
    {
        private readonly List<string> picturePaths;
        private List<Texture> pictures;
        private List<string> nameWords;

        public ArtOption(ModContentPack mod, string path, ArtKind kind, IEnumerable<string> picturePaths)
        {
            Mod = mod;
            Path = path;
            Kind = kind;
            this.picturePaths = picturePaths.ToList();
        }

        /// <summary>The mod the art comes from. For the base game, its art is in the game's Resources.</summary>
        public ModContentPack Mod { get; }

        /// <summary>
        /// The texture path, the folder's path for stack-count and random art, or for facings the path without the
        /// facing's ending.
        /// </summary>
        public string Path { get; }

        public ArtKind Kind { get; }

        public bool IsFolder => Kind == ArtKind.Folder;

        /// <summary>The tile's name: the picture's file name, the folder's name or the facings' shared name.</summary>
        public string Name => Path.Substring(Path.LastIndexOf('/') + 1);

        /// <summary>The words in the tile's name, for finding art that likely matches an item (ArtCatalog.WordsIn).</summary>
        public List<string> NameWords => nameWords ??= ArtCatalog.WordsIn(Name).Distinct().ToList();

        /// <summary>How many pictures the folder holds, 4 for facings, or 1 for a single picture.</summary>
        public int PictureCount => picturePaths.Count;

        /// <summary>The option's pictures in the order the game sorts them (facings: south, east, north, west), loaded on
        /// first use, since 1.7 loads a texture when it's first drawn.</summary>
        public List<Texture> Pictures
        {
            get
            {
                if (pictures == null)
                {
                    List<Texture> loaded = picturePaths.Select(path => (Texture)ArtCatalog.TextureFrom(Mod, path)).ToList();
                    // Facings are labelled by position, so a set missing one shows nothing rather than shifted sides.
                    bool facingMissing = Kind == ArtKind.Facings && loaded.Any(picture => picture == null);
                    pictures = facingMissing ? new List<Texture>() : loaded.Where(picture => picture != null).ToList();
                }
                return pictures;
            }
        }
    }
}
