using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// Shows stack and random art as the set of pictures it is: a strip of three in a tile, and every picture with when
    /// the game uses it while the mouse is over the tile. Four-facing art is its four facings, the front first.
    /// </summary>
    public static class StackPictures
    {
        private const int HoverPanelId = 0x56504C50;
        private const float HoverPictureSize = 72f;
        private const float HoverLabelHeight = 20f;
        private const int HoverColumns = 6;

        private static readonly AccessTools.FieldRef<Graphic_Collection, Graphic[]> SubGraphics =
            AccessTools.FieldRefAccess<Graphic_Collection, Graphic[]>("subGraphics");
        private static readonly Dictionary<Graphic, List<Texture>> picturesByGraphic = new Dictionary<Graphic, List<Texture>>();
        // Facings in the order a set of facings lists them (ArtCatalog): south, east, north, west.
        private const int EastIndex = 1, WestIndex = 3;
        private static readonly string[] FacingKeys = { "VPAP_FacingSouth", "VPAP_FacingEast", "VPAP_FacingNorth", "VPAP_FacingWest" };
        // Draws a picture mirrored left to right.
        private static readonly Rect MirroredCoords = new Rect(1f, 0f, -1f, 1f);

        /// <summary>
        /// A graphic's pictures: each picture of stack or random art, the four facings of four-facing art (south, east,
        /// north, west), or its one picture.
        /// </summary>
        public static List<Texture> Of(Graphic graphic)
        {
            if (graphic == null)
            {
                return new List<Texture>();
            }
            if (!picturesByGraphic.TryGetValue(graphic, out List<Texture> pictures))
            {
                if (graphic is Graphic_Multi multi)
                {
                    pictures = new[] { multi.MatSouth, multi.MatEast, multi.MatNorth, multi.MatWest }
                        .Select(material => material?.mainTexture)
                        .ToList();
                    if (pictures.Any(picture => picture == null))
                    {
                        pictures = new List<Texture>();
                    }
                    picturesByGraphic[graphic] = pictures;
                    return pictures;
                }
                IEnumerable<Graphic> parts = graphic is Graphic_Collection collection
                    ? SubGraphics(collection) ?? Array.Empty<Graphic>()
                    : new[] { graphic };
                pictures = parts.Select(part => part?.MatSingle?.mainTexture).Where(picture => picture != null).ToList();
                picturesByGraphic[graphic] = pictures;
            }
            return pictures;
        }

        /// <summary>Draws up to three of the pictures side by side: the first, the middle and the last.</summary>
        public static void DrawStrip(Rect rect, List<Texture> pictures)
        {
            List<int> shown = ShownIndexes(pictures.Count);
            if (shown.Count == 0)
            {
                return;
            }
            float size = Mathf.Min(rect.height, rect.width / shown.Count);
            float x = rect.center.x - size * shown.Count / 2f;
            foreach (int index in shown)
            {
                Widgets.DrawTextureFitted(new Rect(x, rect.center.y - size / 2f, size, size), pictures[index], 1f);
                x += size;
            }
        }

        /// <summary>
        /// Draws the same up to three pictures as the strip, each with when the item would use it under it, and every
        /// picture while the mouse is over the row.
        /// </summary>
        public static void DrawLabelledRow(Rect rect, List<Texture> pictures, ThingDef item)
        {
            List<int> shown = UsesFacings(item) ? Enumerable.Range(0, pictures.Count).ToList() : ShownIndexes(pictures.Count);
            if (shown.Count < 2)
            {
                return;
            }
            float width = rect.width / shown.Count;
            float size = Mathf.Min(width - 6f, rect.height - 18f);
            for (int i = 0; i < shown.Count; i++)
            {
                var cell = new Rect(rect.x + i * width, rect.y, width, rect.height);
                DrawPicture(new Rect(cell.center.x - size / 2f, cell.y, size, size), pictures, shown[i], item);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = ColorLibrary.Grey;
                Widgets.Label(new Rect(cell.x, cell.y + size, cell.width, 18f), LabelFor(item, shown[i], pictures.Count));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
            HoverPanel(rect, pictures, item);
        }

        // The first, middle and last pictures, or all of them when there are three or fewer.
        private static List<int> ShownIndexes(int count)
        {
            if (count <= 3)
            {
                return Enumerable.Range(0, count).ToList();
            }
            return new List<int> { 0, count / 2, count - 1 };
        }

        /// <summary>While the mouse is over the rect, shows every picture larger with when the item would use it.</summary>
        public static void HoverPanel(Rect rect, List<Texture> pictures, ThingDef item)
        {
            if (pictures.Count < 2 || !Mouse.IsOver(rect))
            {
                return;
            }
            int columns = Mathf.Min(pictures.Count, HoverColumns);
            int rows = (pictures.Count + columns - 1) / columns;
            float cellWidth = HoverPictureSize + 24f;
            var size = new Vector2(columns * cellWidth + 20f, rows * (HoverPictureSize + HoverLabelHeight + 6f) + 20f);
            Vector2 mouse = UI.MousePositionOnUIInverted;
            var panel = new Rect(
                Mathf.Min(mouse.x + 20f, UI.screenWidth - size.x),
                Mathf.Min(mouse.y + 20f, UI.screenHeight - size.y),
                size.x, size.y);
            Find.WindowStack.ImmediateWindow(HoverPanelId, panel, WindowLayer.Super, () =>
            {
                for (int i = 0; i < pictures.Count; i++)
                {
                    float x = 10f + (i % columns) * cellWidth;
                    float y = 10f + (i / columns) * (HoverPictureSize + HoverLabelHeight + 6f);
                    DrawPicture(new Rect(x + 12f, y, HoverPictureSize, HoverPictureSize), pictures, i, item);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(new Rect(x, y + HoverPictureSize, cellWidth, HoverLabelHeight),
                        LabelFor(item, i, pictures.Count));
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
            });
        }

        /// <summary>
        /// When the item would show the picture, following Graphic_StackCount.SubGraphicForStackCount and
        /// Graphic_MealVariants: meal pictures come in three groups (mixed, meat, vegetarian), each by stack size. For
        /// four-facing art, the facing.
        /// </summary>
        public static string LabelFor(ThingDef item, int index, int count)
        {
            if (UsesFacings(item) && index < FacingKeys.Length)
            {
                return FacingKeys[index].Translate();
            }
            Type kind = item.graphicData?.graphicClass;
            if (kind != null && typeof(Graphic_Random).IsAssignableFrom(kind))
            {
                return "VPAP_PictureN".Translate(index + 1);
            }
            if (kind != null && typeof(Graphic_MealVariants).IsAssignableFrom(kind) && count % 3 == 0)
            {
                int sizes = count / 3;
                string group = index / sizes == 0 ? "VPAP_MealMixed".Translate()
                    : index / sizes == 1 ? "VPAP_MealMeat".Translate()
                    : "VPAP_MealVegetarian".Translate();
                return sizes == 1 ? group : group + ", " + StackLabel(index % sizes, sizes);
            }
            return StackLabel(index, count);
        }

        // A side filled from the opposite side is drawn mirrored, as the game draws it when the art allows flipping.
        private static void DrawPicture(Rect rect, List<Texture> pictures, int index, ThingDef item)
        {
            Texture picture = pictures[index];
            bool sidesShared = UsesFacings(item) && item.graphicData.allowFlip && pictures.Count > WestIndex &&
                pictures[EastIndex] == pictures[WestIndex];
            bool mirrored = sidesShared && index == MirroredSide(pictures);
            if (mirrored)
            {
                Widgets.DrawTextureFitted(rect, picture, 1f, new Vector2(picture.width, picture.height), MirroredCoords);
            }
            else
            {
                Widgets.DrawTextureFitted(rect, picture, 1f);
            }
        }

        // Which side the game mirrors: east when the shared picture is the west one, otherwise west.
        private static int MirroredSide(List<Texture> pictures)
        {
            string name = pictures[EastIndex].name;
            return name != null && name.EndsWith("_west", StringComparison.OrdinalIgnoreCase) ? EastIndex : WestIndex;
        }

        private static bool UsesFacings(ThingDef item)
        {
            return item != null && PickRules.UsesFacings(item);
        }

        private static string StackLabel(int index, int count)
        {
            if (index == 0)
            {
                return "VPAP_StackOne".Translate();
            }
            if (count == 2)
            {
                return "VPAP_StackMore".Translate();
            }
            return index == count - 1 ? "VPAP_StackFull".Translate() : "VPAP_StackPartial".Translate();
        }
    }
}
