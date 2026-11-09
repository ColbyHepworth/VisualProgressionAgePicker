using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>The mod settings' list of items with picks, each with buttons to open its picker or reset it.</summary>
    public class ChangedItemsList
    {
        private const float RowHeight = 40f;

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;

        public void Draw(Rect inRect)
        {
            float y = inRect.y;
            Widgets.Label(new Rect(inRect.x, y, 300f, 24f), "VPAP_ChangedItems".Translate());
            search.OnGUI(new Rect(inRect.xMax - 260f, y, 260f, 24f));
            y += 32f;
            bool restartNote = AgePickerMod.ChangedSinceLaunch;
            float listBottom = restartNote ? inRect.yMax - 30f : inRect.yMax;

            List<IGrouping<string, Pick>> items = AgePickerMod.Settings.picks
                .Where(pick => pick?.item != null)
                .GroupBy(pick => pick.item)
                .Where(itemPicks => search.filter.Text.NullOrEmpty() || search.filter.Matches(LabelFor(itemPicks.Key)))
                .OrderBy(itemPicks => LabelFor(itemPicks.Key))
                .ToList();
            var listRect = new Rect(inRect.x, y, inRect.width, listBottom - y);
            if (items.Count == 0)
            {
                GUI.color = ColorLibrary.Grey;
                Widgets.Label(listRect, "VPAP_NoChangedItems".Translate());
                GUI.color = Color.white;
            }
            else
            {
                var view = new Rect(0f, 0f, listRect.width - 16f, items.Count * RowHeight);
                Widgets.BeginScrollView(listRect, ref scroll, view);
                for (int i = 0; i < items.Count; i++)
                {
                    DrawRow(new Rect(0f, i * RowHeight, view.width, RowHeight), items[i].Key, items[i].ToList());
                }
                Widgets.EndScrollView();
            }

            if (restartNote)
            {
                GUI.color = ColorLibrary.Gold;
                Widgets.Label(new Rect(inRect.x, inRect.yMax - 24f, inRect.width, 24f), "VPAP_RestartNote".Translate());
                GUI.color = Color.white;
            }
        }

        private static void DrawRow(Rect rect, string itemName, List<Pick> picks)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            ThingDef item = DefDatabase<ThingDef>.GetNamedSilentFail(itemName);
            Texture icon = item?.graphicData?.Graphic?.MatSingle?.mainTexture;
            if (icon != null)
            {
                Widgets.DrawTextureFitted(new Rect(rect.x + 4f, rect.y + 4f, 32f, 32f), icon, 1f);
            }
            string ages = string.Join(", ", picks.Select(pick => pick.upToAge).Distinct().OrderBy(age => age)
                .Select(age => age.ToStringHuman()));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 44f, rect.y, rect.width - 300f, rect.height),
                LabelFor(itemName).CapitalizeFirst() + "  (" + ages + ")");
            Text.Anchor = TextAnchor.UpperLeft;

            var reset = new Rect(rect.xMax - 110f, rect.y + 6f, 100f, 28f);
            if (Widgets.ButtonText(reset, "VPAP_Reset".Translate()))
            {
                AgePickerMod.Settings.picks.RemoveAll(pick => pick?.item == itemName);
                AgePickerMod.Settings.Write();
                AgePickerMod.ChangedSinceLaunch = true;
            }
            var open = new Rect(reset.x - 110f, rect.y + 6f, 100f, 28f);
            if (item != null && PickRules.CanOpenPicker(item) && Widgets.ButtonText(open, "VPAP_Open".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PickLooks(item));
            }
        }

        private static string LabelFor(string itemName)
        {
            ThingDef item = DefDatabase<ThingDef>.GetNamedSilentFail(itemName);
            return item != null ? PickedLooks.NormalLabelOf(item) : "VPAP_MissingItem".Translate(itemName).ToString();
        }
    }
}
