using System;
using System.Linq;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>Why a pick's art can't be used on an item.</summary>
    public enum ArtBlock
    {
        None,
        /// <summary>The item's art isn't single, stack-count or random art, the kinds a chosen mod's art covers.</summary>
        GraphicKind,
        /// <summary>The item is apparel, and a pick would change only its art on the ground, never the worn art.</summary>
        Apparel,
    }

    /// <summary>Which items can take a look, and which can take art from a chosen mod.</summary>
    public static class PickRules
    {
        // The graphics GraphicData_FromMod can draw from a chosen mod, and the ones built on them, like vanilla meals'.
        private static readonly Type[] ArtFromModGraphics =
        {
            typeof(Graphic_Single), typeof(Graphic_StackCount), typeof(Graphic_Random),
        };

        /// <summary>True when the item has art of its own and can hold a look.</summary>
        public static bool CanHoldLook(ThingDef item)
        {
            return item.graphicData != null && typeof(ThingWithComps).IsAssignableFrom(item.thingClass);
        }

        /// <summary>True for items the picker window offers: things on the ground that can hold a look.</summary>
        public static bool CanOpenPicker(ThingDef item)
        {
            return item.category == ThingCategory.Item && CanHoldLook(item);
        }

        /// <summary>Why art from a chosen mod can't go on the item, or <see cref="ArtBlock.None"/> when it can.</summary>
        public static ArtBlock ArtBlockFor(ThingDef item)
        {
            if (item.IsApparel)
            {
                return ArtBlock.Apparel;
            }
            Type graphicClass = item.graphicData?.graphicClass;
            if (graphicClass == null || !ArtFromModGraphics.Any(kind => kind.IsAssignableFrom(graphicClass)))
            {
                return ArtBlock.GraphicKind;
            }
            return ArtBlock.None;
        }

        /// <summary>True when the item's art is a folder of pictures (stack-count or random art) rather than one picture.</summary>
        public static bool UsesFolder(ThingDef item)
        {
            return item.graphicData?.graphicClass != null &&
                typeof(Graphic_Collection).IsAssignableFrom(item.graphicData.graphicClass);
        }
    }
}
