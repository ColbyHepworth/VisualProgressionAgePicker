using HarmonyLib;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>A "Pick looks" button at the bottom left of an item's info card, which opens the picker for its type.</summary>
    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    public static class InfoCardButton
    {
        private static void Postfix(Rect inRect, Thing ___thing, Def ___def)
        {
            ThingDef item = ___thing?.def ?? ___def as ThingDef;
            if (item == null || !PickRules.CanOpenPicker(item))
            {
                return;
            }
            // On the bottom row, between the stats search box (240 wide at the left) and the centred close button.
            var button = new Rect(inRect.x + 250f, inRect.yMax - 55f, 150f, 40f);
            if (Widgets.ButtonText(button, "VPAP_PickLooks".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickLooks(item));
            }
        }
    }
}
