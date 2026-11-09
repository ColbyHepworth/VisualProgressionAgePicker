using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>
    /// One scrolling grid of art: the item's own art first ("For this item", led by its normal art), then each mod's art
    /// under the mod's name. Only the rows in view are drawn, since 1.7 loads each texture the first time it's drawn.
    /// </summary>
    public class ArtGrid
    {
        private const float PictureTileWidth = 110f;
        private const float FolderTileWidth = 180f;
        private const float ArtHeight = 110f;
        private const float NameBarHeight = 24f;
        private const float TileGap = 10f;
        private const float HeadingHeight = 30f;
        private static readonly Color NameBarColor = new ColorInt(94, 93, 93).ToColor;

        private readonly ThingDef item;
        private readonly List<GridRow> rows = new List<GridRow>();
        private List<ArtOption> itemArt = new List<ArtOption>();
        private List<ArtOption> found = new List<ArtOption>();
        private bool folders;
        private int columns = -1;
        private Vector2 scroll;

        public ArtGrid(ThingDef item)
        {
            this.item = item;
        }

        /// <summary>The art to show: the item's own, then what the search found, as pictures or as folders.</summary>
        public void Show(List<ArtOption> itemOptions, List<ArtOption> foundOptions, bool showFolders)
        {
            itemArt = itemOptions;
            found = foundOptions;
            folders = showFolders;
            columns = -1;
            scroll = Vector2.zero;
        }

        /// <summary>
        /// Draws the grid. Returns true when a tile was clicked this frame, with the option clicked, or null for the normal
        /// art tile.
        /// </summary>
        public bool Draw(Rect rect, List<Texture> normalPictures, bool normalSelected, ArtOption selected,
            out ArtOption clicked)
        {
            clicked = null;
            var tile = new Vector2(folders ? FolderTileWidth : PictureTileWidth, ArtHeight + NameBarHeight);
            float width = rect.width - 16f;
            int fitting = Mathf.Max(1, (int)((width + TileGap) / (tile.x + TileGap)));
            if (fitting != columns)
            {
                columns = fitting;
                BuildRows(tile.y + TileGap);
            }
            float height = 0f;
            foreach (GridRow row in rows)
            {
                height += row.Height;
            }
            bool anyClicked = false;
            Widgets.BeginScrollView(rect, ref scroll, new Rect(0f, 0f, width, height));
            float y = 0f;
            foreach (GridRow row in rows)
            {
                bool inView = y + row.Height >= scroll.y && y <= scroll.y + rect.height;
                if (inView && row.Heading != null)
                {
                    Text.Anchor = TextAnchor.LowerLeft;
                    Widgets.Label(new Rect(0f, y, width, HeadingHeight - 4f), row.Heading);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else if (inView)
                {
                    for (int i = 0; i < row.Tiles.Count; i++)
                    {
                        var tileRect = new Rect(i * (tile.x + TileGap), y, tile.x, tile.y);
                        if (DrawRowTile(tileRect, row.Tiles[i], normalPictures, normalSelected, selected))
                        {
                            anyClicked = true;
                            clicked = row.Tiles[i];
                        }
                    }
                }
                y += row.Height;
            }
            Widgets.EndScrollView();
            return anyClicked;
        }

        /// <summary>The tooltip for an option: its mod and path.</summary>
        public static string TooltipFor(ArtOption option)
        {
            string path = option.IsFolder ? option.Path + "/ (" + "VPLP_Folder".Translate() + ")" : option.Path;
            return option.Mod.Name + "\n" + path;
        }

        // A null option is the item's normal art.
        private bool DrawRowTile(Rect rect, ArtOption option, List<Texture> normalPictures, bool normalSelected,
            ArtOption selected)
        {
            if (option == null)
            {
                string normal = "VPLP_NormalArt".Translate();
                return DrawTile(rect, normalPictures, normal, normal, normalSelected);
            }
            string name = option.IsFolder ? option.Name + " · " + option.PictureCount : option.Name;
            return DrawTile(rect, option.Pictures, name, TooltipFor(option), option == selected);
        }

        // A framed tile with the art inset and a grey name bar along the bottom, as Worldbuilder draws its choices. The
        // selection and hover highlights stay inside the frame.
        private bool DrawTile(Rect rect, List<Texture> pictures, string name, string tooltip, bool selected)
        {
            Widgets.DrawMenuSection(rect);
            Rect art = new Rect(rect.x, rect.y, rect.width, ArtHeight).ContractedBy(5f);
            if (folders)
            {
                StackPictures.DrawStrip(art, pictures);
                StackPictures.HoverPanel(rect, pictures, item);
            }
            else if (pictures.Count > 0)
            {
                Widgets.DrawTextureFitted(art, pictures[0], 1f);
            }
            var nameBar = new Rect(rect.x, rect.yMax - NameBarHeight, rect.width, NameBarHeight);
            Widgets.DrawBoxSolid(nameBar, NameBarColor);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = false;
            Widgets.Label(nameBar.ContractedBy(4f, 0f), name.Truncate(nameBar.width - 8f));
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (selected)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonInvisible(rect);
        }

        private void BuildRows(float tileRowHeight)
        {
            rows.Clear();
            rows.Add(new GridRow { Heading = "VPLP_ForThisItem".Translate(), Height = HeadingHeight });
            var itemTiles = new List<ArtOption> { null };
            itemTiles.AddRange(itemArt);
            AddTileRows(itemTiles, tileRowHeight);

            ModContentPack mod = null;
            var modTiles = new List<ArtOption>();
            foreach (ArtOption option in found)
            {
                if (option.Mod != mod)
                {
                    AddTileRows(modTiles, tileRowHeight);
                    modTiles = new List<ArtOption>();
                    mod = option.Mod;
                    rows.Add(new GridRow { Heading = mod.Name, Height = HeadingHeight });
                }
                modTiles.Add(option);
            }
            AddTileRows(modTiles, tileRowHeight);
            if (found.Count == 0)
            {
                rows.Add(new GridRow { Heading = "VPLP_NoArtFound".Translate(), Height = HeadingHeight });
            }
        }

        private void AddTileRows(List<ArtOption> tiles, float tileRowHeight)
        {
            for (int start = 0; start < tiles.Count; start += columns)
            {
                rows.Add(new GridRow
                {
                    Tiles = tiles.GetRange(start, Mathf.Min(columns, tiles.Count - start)),
                    Height = tileRowHeight,
                });
            }
        }

        private class GridRow
        {
            public string Heading;
            // A null tile is the item's normal art.
            public List<ArtOption> Tiles;
            public float Height;
        }
    }
}
