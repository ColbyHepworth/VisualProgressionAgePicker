using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>Makes a look for each of the player's picks and gives it to the item, beside any looks from packs.</summary>
    public static class PickedLooks
    {
        // The graphics GraphicData_FromMod can draw from a chosen mod, and the ones built on them, like vanilla meals'.
        private static readonly Type[] ArtFromModGraphics =
        {
            typeof(Graphic_Single), typeof(Graphic_StackCount), typeof(Graphic_Random),
        };

        /// <summary>Makes every pick's look. A pick that can't be made logs a warning and leaves its item as it was.</summary>
        public static void MakeAll(bool hotReload)
        {
            IEnumerable<Pick> picks = LookPickerMod.Settings.picks.Where(pick => pick != null);
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
        }

        private static void MakeForItem(string itemName, List<Pick> itemPicks, bool hotReload)
        {
            ThingDef item = DefDatabase<ThingDef>.GetNamedSilentFail(itemName);
            if (item == null)
            {
                Warn($"There's no item named \"{itemName}\", so its picks are skipped.");
                return;
            }
            if (item.graphicData == null)
            {
                Warn($"{item.defName} has no art of its own to change, so its picks are skipped.");
                return;
            }
            if (!typeof(ThingWithComps).IsAssignableFrom(item.thingClass))
            {
                Warn($"{item.defName} can't hold a look, so its picks are skipped.");
                return;
            }
            var picked = new List<AgeLookRow>();
            // The game keeps one def per name, so a second pick for the same age replaces the first.
            foreach (Pick pick in itemPicks.GroupBy(pick => pick.upToAge).Select(sameAge => sameAge.Last()))
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

        private static ThingStyleDef LookFor(ThingDef item, Pick pick, bool hotReload)
        {
            string pickName = $"The pick for {item.defName} up to {pick.upToAge}";
            if (pick.upToAge == TechLevel.Undefined)
            {
                Warn($"A pick for {item.defName} has no age, so it's skipped.");
                return null;
            }
            if (!pick.HasArt && !pick.HasName)
            {
                Warn($"{pickName} has no art and no name, so it's skipped.");
                return null;
            }
            if (!pick.HasArt && !pick.artPath.NullOrEmpty())
            {
                Warn($"{pickName} has an art path but no mod to take it from, so it keeps the item's art.");
            }
            Type graphicClass = item.graphicData.graphicClass;
            if (pick.HasArt && !ArtFromModGraphics.Any(kind => kind.IsAssignableFrom(graphicClass)))
            {
                Warn($"{pickName} names art, but {item.defName}'s art is a {graphicClass?.Name}, and only single, " +
                    "stack-count and random art can come from a chosen mod. It's skipped.");
                return null;
            }
            // From the item and age only, so saves still find the look when its art changes.
            string lookName = $"VPLookPicker_{item.defName}_{pick.upToAge}";
            // A def reload keeps the look made at startup, so items made before and after it still stack.
            ThingStyleDef existing = hotReload ? DefDatabase<ThingStyleDef>.GetNamedSilentFail(lookName) : null;
            if (existing != null)
            {
                return existing;
            }
            GraphicData art = pick.HasArt ? new GraphicData_FromMod() : new GraphicData();
            art.CopyFrom(item.graphicData);
            if (art is GraphicData_FromMod fromMod)
            {
                fromMod.fromMod = pick.artMod;
                if (!pick.artPath.NullOrEmpty())
                {
                    fromMod.texPath = pick.artPath;
                }
            }
            var look = new ThingStyleDef
            {
                defName = lookName,
                overrideLabel = pick.HasName ? pick.name : null,
                graphicData = art,
                modContentPack = LookPickerMod.ContentPack,
            };
            DefGenerator.AddImpliedDef(look, hotReload);
            return look;
        }

        // A pick beats a pack's row for its age. The pack's other rows stay as they were.
        private static void GiveLooks(ThingDef item, List<AgeLookRow> picked)
        {
            CompProperties_AgeLook ageLooks = item.comps.OfType<CompProperties_AgeLook>().FirstOrDefault();
            if (ageLooks == null)
            {
                item.comps.Add(new CompProperties_AgeLook { looks = picked });
                return;
            }
            List<AgeLookRow> packRows = ageLooks.looks ?? new List<AgeLookRow>();
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

        private static void Warn(string message)
        {
            Log.Warning("[Look Picker] " + message);
        }
    }
}
