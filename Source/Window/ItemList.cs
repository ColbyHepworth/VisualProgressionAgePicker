using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// The mod settings' list of items, every pickable item or only the changed ones, searchable by item or mod name.
    /// Clicking an item opens its picker.
    /// </summary>
    public class ItemList
    {
        private const float RowHeight = 40f;
        private const float TabHeight = 32f;

        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private Vector2 scroll;
        private bool showChanged;
        // Every item the picker can open, built on first draw since the defs don't change during a session.
        private List<ListedItem> allItems;
        // All items matching the search, rebuilt when the search changes.
        private List<ListedItem> matchingItems;

        public void Draw(Rect inRect)
        {
            if (allItems == null)
            {
                allItems = DefDatabase<ThingDef>.AllDefs
                    .Where(PickRules.CanOpenPicker)
                    .Select(item => new ListedItem(item.defName, item))
                    .OrderBy(listed => listed.label)
                    .ToList();
            }
            matchingItems ??= allItems.Where(Matches).ToList();

            Dictionary<string, List<TechLevel>> pickedAges = PickedAges();
            List<ListedItem> changedItems = pickedAges.Keys
                .Select(itemName => new ListedItem(itemName, DefDatabase<ThingDef>.GetNamedSilentFail(itemName)))
                .Where(Matches)
                .OrderBy(listed => listed.label)
                .ToList();
            List<ListedItem> shown = showChanged ? changedItems : matchingItems;

            var searchRect = new Rect(inRect.xMax - 260f, inRect.y, 260f, QuickSearchWidget.WidgetHeight);
            search.noResultsMatched = shown.Count == 0;
            search.OnGUI(searchRect, () =>
            {
                matchingItems = allItems.Where(Matches).ToList();
                scroll = Vector2.zero;
            });

            bool restartNote = AgePickerMod.ChangedSinceLaunch;
            var listRect = new Rect(inRect.x, inRect.y + TabHeight + 4f, inRect.width,
                (restartNote ? inRect.yMax - 30f : inRect.yMax) - inRect.y - TabHeight - 4f);
            var tabs = new List<TabRecord>
            {
                new TabRecord("VPAP_AllItemsTab".Translate(matchingItems.Count), () => ShowChanged(false), !showChanged),
                new TabRecord("VPAP_ChangedTab".Translate(changedItems.Count), () => ShowChanged(true), showChanged),
            };
            Widgets.DrawMenuSection(listRect);
            TabDrawer.DrawTabs(listRect, tabs, 200f);

            Rect inner = listRect.ContractedBy(4f);
            if (shown.Count == 0)
            {
                GUI.color = ColorLibrary.Grey;
                Widgets.Label(inner, showChanged && pickedAges.Count == 0
                    ? "VPAP_NoChangedItems".Translate()
                    : "VPAP_NoItemsMatch".Translate());
                GUI.color = Color.white;
            }
            else
            {
                DrawRows(inner, shown, pickedAges);
            }

            if (restartNote)
            {
                GUI.color = ColorLibrary.Gold;
                Widgets.Label(new Rect(inRect.x, inRect.yMax - 24f, inRect.width, 24f), "VPAP_RestartNote".Translate());
                GUI.color = Color.white;
            }
        }

        private void ShowChanged(bool changed)
        {
            showChanged = changed;
            scroll = Vector2.zero;
        }

        private bool Matches(ListedItem listed)
        {
            return search.filter.Text.NullOrEmpty() || search.filter.Matches(listed.label) ||
                (listed.modName != null && search.filter.Matches(listed.modName));
        }

        // Only the rows in view are drawn, since the full list runs to thousands.
        private void DrawRows(Rect rect, List<ListedItem> shown, Dictionary<string, List<TechLevel>> pickedAges)
        {
            var view = new Rect(0f, 0f, rect.width - 16f, shown.Count * RowHeight);
            Widgets.BeginScrollView(rect, ref scroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(shown.Count - 1, Mathf.CeilToInt((scroll.y + rect.height) / RowHeight));
            for (int i = first; i <= last; i++)
            {
                ListedItem listed = shown[i];
                pickedAges.TryGetValue(listed.itemName, out List<TechLevel> ages);
                DrawRow(new Rect(0f, i * RowHeight, view.width, RowHeight), listed, ages);
            }
            Widgets.EndScrollView();
        }

        private static void DrawRow(Rect rect, ListedItem listed, List<TechLevel> ages)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            if (listed.item != null)
            {
                Widgets.DefIcon(new Rect(rect.x + 4f, rect.y + 4f, 32f, 32f), listed.item);
            }

            var reset = new Rect(rect.xMax - 110f, rect.y + 6f, 100f, 28f);
            var agesRect = new Rect(reset.x - 230f, rect.y, 220f, rect.height);
            var nameRect = new Rect(rect.x + 44f, rect.y + 2f, agesRect.x - rect.x - 50f, 22f);
            Widgets.Label(nameRect, listed.label.CapitalizeFirst().Truncate(nameRect.width));
            if (listed.modName != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = ColorLibrary.Grey;
                var modRect = new Rect(nameRect.x, rect.y + 21f, nameRect.width, 18f);
                Widgets.Label(modRect, listed.modName.Truncate(modRect.width));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            if (ages != null)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(agesRect, string.Join(", ", ages.Select(AgeColors.Coloured)));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(reset, "VPAP_Reset".Translate()))
                {
                    AgePickerMod.Settings.picks.RemoveAll(pick => pick?.item == listed.itemName);
                    AgePickerMod.Settings.Write();
                    AgePickerMod.ChangedSinceLaunch = true;
                }
            }

            var open = new Rect(rect.x, rect.y, reset.x - rect.x, rect.height);
            if (listed.item != null && PickRules.CanOpenPicker(listed.item) && Widgets.ButtonInvisible(open))
            {
                Find.WindowStack.Add(new Dialog_PickLooks(listed.item));
            }
        }

        // Each changed item's picked ages, in order, by the item's defName.
        private static Dictionary<string, List<TechLevel>> PickedAges()
        {
            return AgePickerMod.Settings.picks
                .Where(pick => pick?.item != null)
                .GroupBy(pick => pick.item)
                .ToDictionary(itemPicks => itemPicks.Key,
                    itemPicks => itemPicks.Select(pick => pick.upToAge).Distinct().OrderBy(age => age).ToList());
        }

        private class ListedItem
        {
            public readonly string itemName;
            public readonly ThingDef item;
            public readonly string label;
            public readonly string modName;

            public ListedItem(string itemName, ThingDef item)
            {
                this.itemName = itemName;
                this.item = item;
                label = item != null
                    ? PickedLooks.NormalLabelOf(item) ?? item.defName
                    : "VPAP_MissingItem".Translate(itemName).ToString();
                modName = item?.modContentPack?.Name;
            }
        }
    }
}
