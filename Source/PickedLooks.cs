using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>Makes a look for each of the player's picks and gives it to the item, beside any looks from packs.</summary>
    public static class PickedLooks
    {
        private static readonly Dictionary<ThingDef, List<AgeLookRow>> packRowsByItem =
            new Dictionary<ThingDef, List<AgeLookRow>>();
        private static readonly Dictionary<ThingDef, string> normalLabels = new Dictionary<ThingDef, string>();

        /// <summary>The looks packs gave the item, before the player's picks replaced any of them.</summary>
        public static IReadOnlyList<AgeLookRow> PackRowsFor(ThingDef item)
        {
            if (packRowsByItem.TryGetValue(item, out List<AgeLookRow> rows))
            {
                return rows;
            }
            return item.GetCompProperties<CompProperties_AgeLook>()?.looks ?? new List<AgeLookRow>();
        }

        /// <summary>The item's or building's name as its defs give it, before any look renames it for the colony's age.</summary>
        public static string NormalLabelOf(ThingDef item)
        {
            return normalLabels.TryGetValue(item, out string label) ? label : item.label;
        }

        /// <summary>Makes every pick's look. A pick that can't be made logs a warning and leaves its item as it was.</summary>
        public static void MakeAll(bool hotReload)
        {
            if (!hotReload)
            {
                RememberNormalLabels();
            }
            IEnumerable<Pick> picks = AgePickerMod.Settings.picks.Where(pick => pick != null);
            foreach (IGrouping<string, Pick> itemPicks in picks.GroupBy(pick => pick.item ?? ""))
            {
                try
                {
                    MakeForItem(itemPicks.Key, itemPicks.ToList(), hotReload);
                }
                catch (Exception e)
                {
                    Warn($"Couldn't make the picks for \"{itemPicks.Key}\", so it keeps its normal looks: " + e);
                }
            }
            // At startup this waits for every mod's startup code (ItemSymbolsAtStartup), which a reload doesn't run again.
            if (hotReload)
            {
                LongEventHandler.ExecuteWhenFinished(ItemSymbol.KeepAll);
            }
        }

        private static void MakeForItem(string itemName, List<Pick> itemPicks, bool hotReload)
        {
            ThingDef item = DefDatabase<ThingDef>.GetNamedSilentFail(itemName);
            if (item == null)
            {
                Warn($"There's no item named \"{itemName}\", so its picks are skipped.");
                return;
            }
            if (!PickRules.CanHoldLook(item))
            {
                Warn($"{item.defName} has no art of its own or can't hold a look, so its picks are skipped.");
                return;
            }
            var picked = new List<AgeLookRow>();
            foreach (Pick pick in LastPickPerAge(itemPicks))
            {
                ThingStyleDef look = LookFor(item, pick, hotReload);
                if (look != null)
                {
                    picked.Add(new AgeLookRow { upToAge = pick.upToAge, style = look });
                }
            }
            if (picked.Count > 0)
            {
                GiveLooks(item, picked);
            }
        }

        /// <summary>The picks that count for one item: the last one for each age, since a later pick replaces an earlier one.</summary>
        public static IEnumerable<Pick> LastPickPerAge(IEnumerable<Pick> itemPicks)
        {
            return itemPicks.GroupBy(pick => pick.upToAge).Select(sameAge => sameAge.Last());
        }

        /// <summary>Why the pick can't become a look on the item, such as "has no age", or null when it can.</summary>
        public static string ProblemWith(ThingDef item, Pick pick)
        {
            if (pick.upToAge == TechLevel.Undefined)
            {
                return "has no age";
            }
            if (pick.IsEmpty)
            {
                return "has no art and no name";
            }
            ArtBlock artBlock = PickRules.ArtBlockFor(item);
            if (pick.HasArt && artBlock == ArtBlock.GraphicKind)
            {
                return $"names art, but {item.defName}'s art is a {item.graphicData.graphicClass?.Name}" +
                    (item.graphicData.Linked ? " that joins its neighbours" : "") +
                    ", and only single, stack-count, random and four-facing art can come from a chosen mod";
            }
            if (pick.HasArt && artBlock == ArtBlock.Apparel)
            {
                return $"names art, but {item.defName} is apparel, and a pick can't change worn art";
            }
            return null;
        }

        /// <summary>
        /// The graphic data of the pick's look: art from the chosen mod, the item's own art, or for a pick without art the
        /// art the age had. In that last case the pack's look covering the age is given too, since the pick keeps it.
        /// </summary>
        public static GraphicData ArtFor(ThingDef item, Pick pick, IReadOnlyList<AgeLookRow> packRows,
            out ThingStyleDef keptPackLook)
        {
            keptPackLook = pick.HasArt || pick.normalArt ? null : PackLookCovering(packRows, pick.upToAge);
            return pick.HasArt ? ArtFromMod(item, pick) : CopyOf(keptPackLook?.graphicData ?? item.graphicData);
        }

        private static ThingStyleDef LookFor(ThingDef item, Pick pick, bool hotReload)
        {
            string pickName = $"The pick for {item.defName} up to {pick.upToAge}";
            string problem = ProblemWith(item, pick);
            if (problem != null)
            {
                Warn($"{pickName} {problem}, so it's skipped.");
                return null;
            }
            if (!pick.HasArt && !pick.artPath.NullOrEmpty())
            {
                Warn($"{pickName} has an art path but no mod to take it from, so it keeps the item's art.");
            }
            // From the item and age only, so saves still find the look when its art changes.
            string lookName = $"VPAgePicker_{item.defName}_{pick.upToAge}";
            // A def reload keeps the look made at startup, so items made before and after it still stack.
            ThingStyleDef existing = hotReload ? DefDatabase<ThingStyleDef>.GetNamedSilentFail(lookName) : null;
            if (existing != null)
            {
                return existing;
            }
            // The packs' looks only: the picks join the item's list after all its looks are made.
            List<AgeLookRow> packRows = item.GetCompProperties<CompProperties_AgeLook>()?.looks;
            GraphicData art = ArtFor(item, pick, packRows, out ThingStyleDef packLook);
            var look = new ThingStyleDef
            {
                defName = lookName,
                overrideLabel = pick.HasName ? pick.name : null,
                graphicData = art,
                modContentPack = AgePickerMod.ContentPack,
            };
            if (packLook != null)
            {
                look.wornGraphicPath = packLook.wornGraphicPath;
                look.uiIconPath = packLook.uiIconPath;
                look.uiIconScale = packLook.uiIconScale;
                look.color = packLook.color;
            }
            DefGenerator.AddImpliedDef(look, hotReload);
            if (pick.HasArt)
            {
                ItemSymbol.KeepLater(item, look, pick.artMod);
            }
            return look;
        }

        private static GraphicData ArtFromMod(ThingDef item, Pick pick)
        {
            var art = new GraphicData_FromMod();
            art.CopyFrom(item.graphicData);
            art.fromMod = pick.artMod;
            if (!pick.artPath.NullOrEmpty())
            {
                art.texPath = pick.artPath;
            }
            if (pick.singlePicture)
            {
                // One picture at every stack size; a thing draws its style's own graphic data.
                art.graphicClass = typeof(Graphic_Single);
            }
            return art;
        }

        private static GraphicData CopyOf(GraphicData source)
        {
            GraphicData copy = source is GraphicData_FromMod fromMod
                ? fromMod.CopyArtSource()
                : new GraphicData();
            copy.CopyFrom(source);
            return copy;
        }

        // The pack's look that covers the age, or null when no pack's look does.
        private static ThingStyleDef PackLookCovering(IReadOnlyList<AgeLookRow> packRows, TechLevel age)
        {
            List<AgeLookRow> rows = packRows?.Where(row => row?.style != null).ToList();
            if (rows == null)
            {
                return null;
            }
            int row = AgeLookTable.RowFor(rows.Select(packRow => (int)packRow.upToAge).ToList(), (int)age);
            return row < 0 ? null : rows[row].style;
        }

        // A pick beats a pack's row for its age. The pack's other rows stay as they were.
        private static void GiveLooks(ThingDef item, List<AgeLookRow> picked)
        {
            CompProperties_AgeLook ageLooks = item.comps.OfType<CompProperties_AgeLook>().FirstOrDefault();
            if (ageLooks == null)
            {
                packRowsByItem[item] = new List<AgeLookRow>();
                item.comps.Add(new CompProperties_AgeLook { looks = picked });
                return;
            }
            List<AgeLookRow> packRows = ageLooks.looks ?? new List<AgeLookRow>();
            packRowsByItem[item] = new List<AgeLookRow>(packRows);
            List<AgeRow> merged = LookRows.WithPicks(
                packRows.Select(row => new AgeRow((int)row.upToAge, row.style?.defName)).ToList(),
                picked.Select(row => new AgeRow((int)row.upToAge, row.style.defName)).ToList());
            // Pack rows are kept as they are, since a style the game hasn't resolved yet is filled in on its row later.
            var rows = new List<AgeLookRow>();
            var unusedPackRows = new List<AgeLookRow>(packRows);
            foreach (AgeRow row in merged)
            {
                AgeLookRow pickRow = picked.FirstOrDefault(pick => pick.style.defName == row.Look);
                if (pickRow != null)
                {
                    rows.Add(pickRow);
                    continue;
                }
                AgeLookRow packRow = unusedPackRows.First(pack =>
                    (int)pack.upToAge == row.UpToAge && pack.style?.defName == row.Look);
                unusedPackRows.Remove(packRow);
                rows.Add(packRow);
            }
            ageLooks.looks = rows;
        }

        private static void RememberNormalLabels()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                // The framework renames a listed type for the colony's age, buildings included.
                if (PickRules.CanOpenPicker(def))
                {
                    normalLabels[def] = def.label;
                }
            }
        }

        private static void Warn(string message)
        {
            Log.Warning("[Age Picker] " + message);
        }
    }
}
