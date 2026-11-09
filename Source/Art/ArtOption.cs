using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>One picture, or one folder of pictures, a mod has that a pick can use.</summary>
    public sealed class ArtOption
    {
        private readonly List<string> picturePaths;
        private List<Texture> pictures;

        public ArtOption(ModContentPack mod, string path, bool isFolder, IEnumerable<string> picturePaths)
        {
            Mod = mod;
            Path = path;
            IsFolder = isFolder;
            this.picturePaths = picturePaths.ToList();
        }

        /// <summary>The mod the art comes from. For the base game, its art is in the game's Resources.</summary>
        public ModContentPack Mod { get; }

        /// <summary>The texture path, or the folder's path for stack-count and random art.</summary>
        public string Path { get; }

        public bool IsFolder { get; }

        /// <summary>The tile's name: the picture's file name or the folder's name.</summary>
        public string Name => Path.Substring(Path.LastIndexOf('/') + 1);

        /// <summary>How many pictures the folder holds, or 1 for a single picture.</summary>
        public int PictureCount => picturePaths.Count;

        /// <summary>The option's pictures in the order the game sorts them, loaded on first use, since 1.7 loads a texture
        /// when it's first drawn.</summary>
        public List<Texture> Pictures
        {
            get
            {
                pictures ??= picturePaths
                    .Select(path => (Texture)ArtCatalog.TextureFrom(Mod, path))
                    .Where(picture => picture != null)
                    .ToList();
                return pictures;
            }
        }
    }
}
