using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>A pack made from the player's picks, ready to write: its files, the mods it needs and the picks it left out.</summary>
    public class ExportedPack
    {
        public string folderName;
        public string packageId;
        public XDocument about;
        public XDocument loadFolders;
        public XDocument looks;
        public List<string> dependencies = new List<string>();
        public List<string> leftOut = new List<string>();
    }

    /// <summary>
    /// Writes the player's picks as a pack like the framework's: one look per age and an ItemLooksDef per item. Art from
    /// another mod stays in that mod, which the pack lists as a dependency, so a pack never holds other people's art.
    /// </summary>
    public static class PackExport
    {
        private const string FrameworkId = "groovy.visualprogression.framework";
        private const string LooksPath = "1.7/Defs/Looks.xml";
        // The game takes a packageId of up to 60 letters, digits and dots (ModMetaData.PackageIdFormatRegex).
        private const int PackageIdPartLength = 29;

        /// <summary>Makes the pack's files from the picks without touching the disk.</summary>
        public static ExportedPack Build(string packName, string author, IEnumerable<Pick> picks)
        {
            // The author keeps two players' packs with the same name from sharing look names.
            string prefix = LettersAndDigits(author) + "_" + LettersAndDigits(packName);
            var pack = new ExportedPack
            {
                folderName = GenFile.SanitizedFileName(packName.Trim()),
                packageId = PackageIdPart(author) + "." + PackageIdPart(packName),
            };
            var needed = new List<string> { FrameworkId };
            var loadAfter = new List<string> { FrameworkId };
            var defs = new XElement("Defs");
            var itemLabels = new List<string>();

            foreach (IGrouping<string, Pick> itemPicks in picks.Where(pick => pick?.item != null).GroupBy(pick => pick.item))
            {
                ThingDef item = DefDatabase<ThingDef>.GetNamedSilentFail(itemPicks.Key);
                if (item == null || !PickRules.CanHoldLook(item))
                {
                    pack.leftOut.Add("VPAP_LeftOutItem".Translate(itemPicks.Key));
                    continue;
                }
                // An earlier export of this pack is replaced, so its rows don't count.
                List<AgeLookRow> packRows = PickedLooks.PackRowsFor(item)
                    .Where(row => !SamePackage(row?.style?.modContentPack?.PackageIdPlayerFacing, pack.packageId))
                    .ToList();
                string itemMod = OwnModOf(item);
                string itemLabel = PickedLooks.NormalLabelOf(item).CapitalizeFirst();
                var rows = new XElement("looks");
                foreach (Pick pick in PickedLooks.LastPickPerAge(itemPicks).OrderBy(pick => pick.upToAge))
                {
                    string problem = PickedLooks.ProblemWith(item, pick) ?? UnwritableArt(item, pick, packRows);
                    if (problem != null)
                    {
                        pack.leftOut.Add($"{itemLabel}, {pick.upToAge.ToStringHuman()}: {problem}");
                        continue;
                    }
                    string lookName = $"{prefix}_{item.defName}_{pick.upToAge}";
                    defs.Add(LookElement(item, pick, packRows, lookName, itemMod, needed));
                    rows.Add(new XElement("li", new XElement("upToAge", pick.upToAge), new XElement("style", lookName)));
                }
                if (!rows.HasElements)
                {
                    continue;
                }
                var itemLooks = new XElement("VisualProgression.ItemLooksDef",
                    new XElement("defName", $"{prefix}_{item.defName}"),
                    new XElement("item", item.defName),
                    rows);
                RequireMod(itemLooks, itemMod);
                defs.Add(itemLooks);
                itemLabels.Add(itemLabel);
                // The pack's rows beat other packs' rows for the same age only when it loads after them.
                loadAfter.AddRange(packRows.Select(row => row?.style?.modContentPack?.PackageIdPlayerFacing));
                loadAfter.Add(itemMod);
            }

            pack.dependencies = OtherMods(needed, pack.packageId);
            loadAfter.AddRange(pack.dependencies);
            pack.looks = new XDocument(new XDeclaration("1.0", "utf-8", null), defs);
            pack.about = AboutFor(packName, author, pack.packageId, itemLabels, pack.dependencies,
                OtherMods(loadAfter, pack.packageId));
            pack.loadFolders = new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement("loadFolders", new XElement("v1.7", new XElement("li", "/"), new XElement("li", "1.7"))));
            return pack;
        }

        /// <summary>
        /// Why the pack can't be written to its folder, or null when it can: a name that isn't a folder in the Mods
        /// folder, or a folder holding another mod.
        /// </summary>
        public static string FolderProblem(ExportedPack pack)
        {
            string folder = FolderFor(pack);
            string modsFolder = Path.GetFullPath(GenFilePaths.ModsFolderPath).TrimEnd(Path.DirectorySeparatorChar);
            if (!pack.folderName.Any(char.IsLetterOrDigit) ||
                Path.GetDirectoryName(Path.GetFullPath(folder)) != modsFolder)
            {
                return "VPAP_BadPackName".Translate();
            }
            string aboutFile = Path.Combine(folder, "About", "About.xml");
            if (File.Exists(aboutFile))
            {
                string existingId = XDocument.Load(aboutFile).Root?.Element("packageId")?.Value?.Trim();
                if (!SamePackage(existingId, pack.packageId))
                {
                    return "VPAP_FolderTaken".Translate(pack.folderName);
                }
            }
            return null;
        }

        /// <summary>
        /// Writes the pack's three files into the folder, replacing earlier copies of them. Nothing else in the folder is
        /// changed.
        /// </summary>
        public static void WriteTo(ExportedPack pack, string folder)
        {
            Save(pack.about, Path.Combine(folder, "About", "About.xml"));
            Save(pack.loadFolders, Path.Combine(folder, "LoadFolders.xml"));
            Save(pack.looks, Path.Combine(folder, LooksPath));
        }

        /// <summary>The folder in the game's Mods folder the pack is written to.</summary>
        public static string FolderFor(ExportedPack pack)
        {
            return Path.Combine(GenFilePaths.ModsFolderPath, pack.folderName);
        }

        private static XElement LookElement(ThingDef item, Pick pick, IReadOnlyList<AgeLookRow> packRows, string lookName,
            string itemMod, List<string> needed)
        {
            GraphicData art = PickedLooks.ArtFor(item, pick, packRows, out ThingStyleDef keptPackLook);
            if (keptPackLook != null && KeepsPackArt(keptPackLook))
            {
                // The pack's art can come from any mod it needs, so the export needs the pack rather than naming one mod.
                needed.Add(keptPackLook.modContentPack?.PackageIdPlayerFacing);
            }
            string fromMod = (art as GraphicData_FromMod)?.fromMod;
            // The item's own mod is already required through MayRequire.
            if (pick.HasArt && !SamePackage(fromMod, itemMod))
            {
                needed.Add(fromMod);
            }

            var look = new XElement("ThingStyleDef", new XElement("defName", lookName));
            RequireMod(look, itemMod);
            if (pick.HasName)
            {
                look.Add(new XElement("overrideLabel", pick.name));
            }
            // Without graphic data the look shows the item's own art in the player's game, patches and retextures included.
            if (WritesArt(pick, keptPackLook))
            {
                look.Add(LookXml.GraphicDataElement(art));
            }
            if (keptPackLook != null)
            {
                AddIfSet(look, "wornGraphicPath", keptPackLook.wornGraphicPath);
                AddIfSet(look, "uiIconPath", keptPackLook.uiIconPath);
                if (keptPackLook.uiIconScale != 1f)
                {
                    look.Add(new XElement("uiIconScale", LookXml.TextOf(keptPackLook.uiIconScale)));
                }
                if (keptPackLook.color != default)
                {
                    look.Add(new XElement("color", LookXml.TextOf(keptPackLook.color)));
                }
            }
            return look;
        }

        // Art from a chosen mod or a pack's look is written. The item's own art isn't, so it stays the player's own.
        private static bool WritesArt(Pick pick, ThingStyleDef keptPackLook)
        {
            return pick.HasArt || keptPackLook?.graphicData != null;
        }

        // Why the pick's art can't be written, or null when it can.
        private static string UnwritableArt(ThingDef item, Pick pick, IReadOnlyList<AgeLookRow> packRows)
        {
            GraphicData art = PickedLooks.ArtFor(item, pick, packRows, out ThingStyleDef keptPackLook);
            if (!WritesArt(pick, keptPackLook))
            {
                return null;
            }
            string field = LookXml.UnwritableField(art);
            return field == null ? null : "VPAP_UnwritableArt".Translate(field).ToString();
        }

        // A rename keeps the pack's art, worn art or icon, so the pack has to be active for them to show.
        private static bool KeepsPackArt(ThingStyleDef packLook)
        {
            return packLook.graphicData != null || !packLook.wornGraphicPath.NullOrEmpty() ||
                !packLook.uiIconPath.NullOrEmpty();
        }

        // The mods named, without the base game, the pack itself or repeats.
        private static List<string> OtherMods(IEnumerable<string> packageIds, string ownId)
        {
            return packageIds
                .Where(id => id != null && !IsBaseGame(id) && !SamePackage(id, ownId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool SamePackage(string first, string second)
        {
            return first != null && second != null && string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static XDocument AboutFor(string packName, string author, string packageId, List<string> itemLabels,
            List<string> dependencies, List<string> loadAfter)
        {
            var description = new StringBuilder();
            description.Append("VPAP_PackDescription".Translate(AgePickerMod.ContentPack.Name));
            description.Append("\n\n");
            description.Append(string.Join(", ", itemLabels.OrderBy(label => label).Select(label => label.CapitalizeFirst())));

            var modDependencies = new XElement("modDependencies");
            foreach (string id in dependencies)
            {
                modDependencies.Add(DependencyElement(id));
            }
            var about = new XElement("ModMetaData",
                new XElement("name", packName.Trim()),
                new XElement("author", author.Trim()),
                new XElement("packageId", packageId),
                new XElement("supportedVersions", new XElement("li", "1.7")),
                new XElement("description", description.ToString()),
                modDependencies,
                new XElement("loadAfter", loadAfter.Select(id => new XElement("li", id))));
            return new XDocument(new XDeclaration("1.0", "utf-8", null), about);
        }

        private static XElement DependencyElement(string packageId)
        {
            ModMetaData mod = ModLister.GetModWithIdentifier(packageId, ignorePostfix: true);
            var element = new XElement("li",
                new XElement("packageId", packageId),
                new XElement("displayName", mod?.Name ?? packageId));
            string workshopId = mod != null ? WorkshopIdOf(mod) : null;
            if (workshopId != null)
            {
                element.Add(new XElement("steamWorkshopUrl", "steam://url/CommunityFilePage/" + workshopId));
            }
            else if (!(mod?.Url).NullOrEmpty())
            {
                element.Add(new XElement("downloadUrl", mod.Url));
            }
            return element;
        }

        // A mod uploaded to the Workshop keeps its page's ID in About/PublishedFileId.txt. One installed from the Workshop
        // without that file sits in a folder named after the ID.
        private static string WorkshopIdOf(ModMetaData mod)
        {
            if (mod.OnSteamWorkshop && ulong.TryParse(mod.FolderName, out _))
            {
                return mod.FolderName;
            }
            string idFile = Path.Combine(mod.RootDir.FullName, "About", "PublishedFileId.txt");
            if (File.Exists(idFile))
            {
                string id = File.ReadAllText(idFile).Trim();
                if (ulong.TryParse(id, out _))
                {
                    return id;
                }
            }
            return null;
        }

        // The packageId of the mod whose Defs add the item, or null for the base game's items and items a patch adds.
        private static string OwnModOf(ThingDef item)
        {
            ModContentPack mod = item.modContentPack;
            return mod == null || mod.IsCoreMod ? null : mod.PackageIdPlayerFacing;
        }

        private static void RequireMod(XElement element, string packageId)
        {
            if (packageId != null)
            {
                element.SetAttributeValue("MayRequire", packageId);
            }
        }

        private static void AddIfSet(XElement element, string name, string value)
        {
            if (!value.NullOrEmpty())
            {
                element.Add(new XElement(name, value));
            }
        }

        private static bool IsBaseGame(string packageId)
        {
            return SamePackage(packageId, ModContentPack.CoreModPackageId);
        }

        // Defs and packageIds take plain letters and digits only.
        private static string LettersAndDigits(string text)
        {
            string kept = new string((text ?? "").Where(c => c < 128 && char.IsLetterOrDigit(c)).ToArray());
            return kept.Length > 0 ? kept : "Pack";
        }

        private static string PackageIdPart(string text)
        {
            string part = LettersAndDigits(text).ToLowerInvariant();
            return part.Length > PackageIdPartLength ? part.Substring(0, PackageIdPartLength) : part;
        }

        private static void Save(XDocument document, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            document.Save(path);
        }
    }
}
