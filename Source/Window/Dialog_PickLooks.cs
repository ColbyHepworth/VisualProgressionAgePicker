using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// Picks one item's art and name per age. The left column shows the selected age's look, the ages and the name; the
    /// right side is one grid of art to click. The picks are saved when the window closes and take effect after a
    /// restart.
    /// </summary>
    public class Dialog_PickLooks : Window
    {
        private static readonly TechLevel[] Ages =
        {
            TechLevel.Neolithic, TechLevel.Medieval, TechLevel.Industrial, TechLevel.Spacer, TechLevel.Ultra,
        };

        private const float TitleHeight = 40f;
        private const float LeftWidth = 230f;
        private const float Spacing = 15f;
        private const float PreviewSize = 200f;
        private const float AgeRowHeight = 40f;
        private const float RowHeight = 30f;

        private readonly ThingDef item;
        private readonly ArtBlock artBlock;
        private readonly bool usesFolder;
        private readonly Dictionary<TechLevel, Pick> picksByAge;
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private readonly ArtGrid grid;
        private readonly Dictionary<ArtKind, List<ArtOption>> forItem = new Dictionary<ArtKind, List<ArtOption>>();
        private readonly Dictionary<ArtKind, List<ArtOption>> found = new Dictionary<ArtKind, List<ArtOption>>();
        // Stack, random and meal items choose between whole folders and single pictures; four-facing art offers sets of
        // facings; other items offer pictures.
        private ArtKind kind;
        private TechLevel selectedAge;
        private bool changed;

        public Dialog_PickLooks(ThingDef item)
        {
            this.item = item;
            artBlock = PickRules.ArtBlockFor(item);
            usesFolder = PickRules.UsesFolder(item);
            grid = new ArtGrid(item);
            picksByAge = new Dictionary<TechLevel, Pick>();
            // The game keeps the last pick for an age, as PickedLooks does.
            foreach (Pick pick in AgePickerMod.Settings.picks.Where(pick => pick?.item == item.defName))
            {
                picksByAge[pick.upToAge] = new Pick
                {
                    item = pick.item, upToAge = pick.upToAge, artMod = pick.artMod, artPath = pick.artPath, name = pick.name,
                    singlePicture = pick.singlePicture, normalArt = pick.normalArt,
                };
            }
            selectedAge = StartingAge();
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(1000f, 760f);

        public override void PreOpen()
        {
            base.PreOpen();
            if (artBlock == ArtBlock.None)
            {
                kind = ArtCatalog.KindFor(item, singlePicture: false);
                foreach (ArtKind offered in OfferedKinds())
                {
                    forItem[offered] = ArtCatalog.ForItem(item, offered);
                }
                FindArt();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!changed)
            {
                return;
            }
            List<Pick> picks = AgePickerMod.Settings.picks;
            picks.RemoveAll(pick => pick == null || pick.item == item.defName);
            foreach (Pick pick in picksByAge.Values)
            {
                pick.name = pick.name?.Trim();
            }
            picks.AddRange(picksByAge.Values.Where(pick => !pick.IsEmpty).OrderBy(pick => pick.upToAge));
            AgePickerMod.Settings.Write();
            AgePickerMod.ChangedSinceLaunch = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 30f, TitleHeight),
                "VPAP_Title".Translate(PickedLooks.NormalLabelOf(item).CapitalizeFirst()));
            Text.Font = GameFont.Small;

            var left = new Rect(inRect.x, inRect.y + TitleHeight, LeftWidth, inRect.height - TitleHeight);
            DrawLeftColumn(left);
            var right = new Rect(left.xMax + Spacing, left.y, inRect.xMax - left.xMax - Spacing, left.height);
            DrawRightSide(right);
        }

        // The colony's age while a game runs, moved to the nearest age shown; Medieval otherwise.
        private static TechLevel StartingAge()
        {
            if (Current.ProgramState != ProgramState.Playing || Faction.OfPlayer == null)
            {
                return TechLevel.Medieval;
            }
            TechLevel colonyAge = Faction.OfPlayer.def.techLevel;
            if (colonyAge < Ages[0])
            {
                return Ages[0];
            }
            return colonyAge > Ages[Ages.Length - 1] ? Ages[Ages.Length - 1] : colonyAge;
        }

        // The selected age's look, the stack pictures it uses, the ages and the name. Nothing here scrolls.
        private void DrawLeftColumn(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(Spacing);
            float y = inner.y;
            AgeLook look = LookFor(selectedAge);

            var preview = new Rect(inner.center.x - PreviewSize / 2f, y, PreviewSize, PreviewSize);
            Widgets.DrawMenuSection(preview);
            if (look.Pictures.Count > 0)
            {
                Widgets.DrawTextureFitted(preview.ContractedBy(PreviewSize * 0.05f), look.Pictures[0], 1f);
            }
            y += PreviewSize + 4f;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inner.x, y, inner.width, 24f), look.Name.CapitalizeFirst());
            Text.Anchor = TextAnchor.UpperLeft;
            y += 26f;
            if (look.Pictures.Count > 1)
            {
                StackPictures.DrawLabelledRow(new Rect(inner.x, y, inner.width, 74f), look.Pictures, item);
                y += 80f;
            }

            foreach (TechLevel age in Ages)
            {
                DrawAgeRow(new Rect(inner.x, y, inner.width, AgeRowHeight), age);
                y += AgeRowHeight + 2f;
            }
            y += 8f;
            DrawNameField(new Rect(inner.x, y, inner.width, RowHeight));
            if (changed || AgePickerMod.ChangedSinceLaunch)
            {
                GUI.color = ColorLibrary.Gold;
                Widgets.Label(new Rect(inner.x, inner.yMax - 24f, inner.width, 24f), "VPAP_RestartNote".Translate());
                GUI.color = Color.white;
            }
        }

        private void DrawAgeRow(Rect rect, TechLevel age)
        {
            Widgets.DrawOptionBackground(rect, age == selectedAge);
            AgeLook look = LookFor(age);
            if (look.Pictures.Count > 0)
            {
                Widgets.DrawTextureFitted(new Rect(rect.x + 4f, rect.y + 4f, 32f, 32f), look.Pictures[0], 1f);
            }
            GUI.color = AgeColors.For(age);
            Widgets.Label(new Rect(rect.x + 42f, rect.y + 2f, rect.width - 46f, 22f), age.ToStringHuman().CapitalizeFirst());
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            var nameLine = new Rect(rect.x + 42f, rect.y + 21f, rect.width - 46f, 18f);
            string plain = look.NameLine(age, coloured: false);
            if (Text.CalcSize(plain).x <= nameLine.width)
            {
                Widgets.Label(nameLine, look.NameLine(age, coloured: true));
            }
            else
            {
                Widgets.Label(nameLine, plain.Truncate(nameLine.width));
                TooltipHandler.TipRegion(rect, plain);
            }
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(rect))
            {
                selectedAge = age;
            }
        }

        private void DrawNameField(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, 50f, rect.height), "VPAP_Name".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            picksByAge.TryGetValue(selectedAge, out Pick pick);
            string current = pick?.name ?? "";
            string typed = Widgets.TextField(new Rect(rect.x + 50f, rect.y, rect.width - 50f, rect.height), current);
            if (typed != current)
            {
                PickAt(selectedAge).name = typed.Trim().NullOrEmpty() ? null : typed;
                changed = true;
            }
            if (pick != null && !pick.IsEmpty &&
                Widgets.ButtonText(new Rect(rect.x, rect.yMax + 8f, rect.width, RowHeight), "VPAP_ResetAge".Translate()))
            {
                picksByAge.Remove(selectedAge);
                changed = true;
            }
        }

        // The art toggle and search, one grid filling the height, and the hint at the bottom.
        private void DrawRightSide(Rect rect)
        {
            const float BottomHeight = 26f;
            var bottom = new Rect(rect.x, rect.yMax - BottomHeight + 4f, rect.width, BottomHeight - 4f);
            if (artBlock != ArtBlock.None)
            {
                string reason = artBlock == ArtBlock.Apparel
                    ? "VPAP_NamesOnlyApparel".Translate()
                    : "VPAP_NamesOnlyGraphic".Translate();
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 60f), reason);
                return;
            }

            if (usesFolder)
            {
                DrawKindChip(new Rect(rect.x, rect.y, 190f, 28f), ArtKind.Folder, "VPAP_WholeFolders");
                DrawKindChip(new Rect(rect.x + 200f, rect.y, 190f, 28f), ArtKind.Picture, "VPAP_SinglePictures");
            }
            search.OnGUI(new Rect(rect.xMax - 260f, rect.y + 2f, 260f, 24f), FindArt);

            picksByAge.TryGetValue(selectedAge, out Pick pick);
            bool normalChosen = pick != null && pick.normalArt;
            ArtOption chosen = pick != null && pick.HasArt ? PickedOption(pick) : null;
            var gridRect = new Rect(rect.x, rect.y + 36f, rect.width, bottom.y - rect.y - 40f);
            Widgets.DrawMenuSection(gridRect);
            if (grid.Draw(gridRect.ContractedBy(8f), NormalPictures(), normalChosen, chosen, out ArtOption clicked))
            {
                PutArt(clicked);
            }
            GUI.color = ColorLibrary.Grey;
            Widgets.Label(bottom, "VPAP_Hint".Translate());
            GUI.color = Color.white;
        }

        private IEnumerable<ArtKind> OfferedKinds()
        {
            if (PickRules.UsesFacings(item))
            {
                return new[] { ArtKind.Facings };
            }
            return usesFolder ? new[] { ArtKind.Folder, ArtKind.Picture } : new[] { ArtKind.Picture };
        }

        // Runs the search for every kind offered, so each chip can show its count.
        private void FindArt()
        {
            foreach (ArtKind offered in OfferedKinds())
            {
                found[offered] = ArtCatalog.Search(item, search.filter.Text, offered);
            }
            ShowKind(kind);
        }

        private void ShowKind(ArtKind shown)
        {
            kind = shown;
            grid.Show(forItem[kind], found[kind], kind);
        }

        private void DrawKindChip(Rect rect, ArtKind chipKind, string labelKey)
        {
            Widgets.DrawOptionBackground(rect, kind == chipKind);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, labelKey.Translate() + " (" + found[chipKind].Count + ")");
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect) && kind != chipKind)
            {
                ShowKind(chipKind);
            }
        }

        // Null puts the item's normal art on the selected age, even where a pack's look would cover it.
        private void PutArt(ArtOption option)
        {
            Pick pick = PickAt(selectedAge);
            if (option == null)
            {
                pick.artMod = null;
                pick.artPath = null;
                pick.singlePicture = false;
                pick.normalArt = true;
            }
            else
            {
                pick.normalArt = false;
                pick.artMod = option.Mod.PackageIdPlayerFacing;
                pick.artPath = option.Path == item.graphicData.texPath?.TrimEnd('/') ? null : option.Path;
                pick.singlePicture = usesFolder && !option.IsFolder;
            }
            changed = true;
        }

        private ArtOption PickedOption(Pick pick)
        {
            return ArtCatalog.Find(pick.artMod, PickedPath(pick), ArtCatalog.KindFor(item, pick.singlePicture));
        }

        private Pick PickAt(TechLevel age)
        {
            if (!picksByAge.TryGetValue(age, out Pick pick))
            {
                pick = new Pick { item = item.defName, upToAge = age };
                picksByAge[age] = pick;
            }
            return pick;
        }

        // What the item will look like in this age after a restart: the framework's rule that a row covers its age and the
        // earlier ones, over the packs' rows with the picks in place of theirs.
        private AgeLook LookFor(TechLevel age)
        {
            // A pick replaces the packs' rows for its age, as PickedLooks does.
            List<Pick> picks = picksByAge.Values.Where(pick => !pick.IsEmpty).ToList();
            List<AgeLookRow> packRows = PickedLooks.PackRowsFor(item)
                .Where(row => row?.style != null && picks.All(pick => pick.upToAge != row.upToAge))
                .ToList();
            List<int> rowAges = picks.Select(pick => (int)pick.upToAge)
                .Concat(packRows.Select(row => (int)row.upToAge))
                .ToList();
            int rowIndex = AgeLookTable.RowFor(rowAges, (int)age);
            if (rowIndex < 0)
            {
                return new AgeLook(NormalPictures(), PickedLooks.NormalLabelOf(item), LookSource.Normal, age);
            }
            var rowAge = (TechLevel)rowAges[rowIndex];
            List<Texture> art;
            string name;
            LookSource source;
            if (rowIndex < picks.Count)
            {
                Pick pick = picks[rowIndex];
                // A pick without art keeps the art the age had, as PickedLooks does.
                if (pick.HasArt)
                {
                    art = PickedPictures(pick);
                }
                else
                {
                    art = pick.normalArt ? NormalPictures() : PackPicturesCovering(age) ?? NormalPictures();
                }
                name = pick.name ?? PickedLooks.NormalLabelOf(item);
                source = LookSource.Pick;
            }
            else
            {
                ThingStyleDef style = packRows[rowIndex - picks.Count].style;
                art = style.graphicData != null
                    ? StackPictures.Of(style.graphicData.Graphic)
                    : new List<Texture> { style.UIIcon };
                name = style.overrideLabel ?? PickedLooks.NormalLabelOf(item);
                source = LookSource.Pack;
            }
            return new AgeLook(art, name, source, rowAge);
        }

        // The pictures of the pack's look covering the age (the normal art for a look with only a name), or null when no
        // pack's look does.
        private List<Texture> PackPicturesCovering(TechLevel age)
        {
            List<AgeLookRow> rows = PickedLooks.PackRowsFor(item).Where(row => row?.style != null).ToList();
            int row = AgeLookTable.RowFor(rows.Select(packRow => (int)packRow.upToAge).ToList(), (int)age);
            if (row < 0)
            {
                return null;
            }
            GraphicData packArt = rows[row].style.graphicData;
            return packArt != null ? StackPictures.Of(packArt.Graphic) : NormalPictures();
        }

        private string PickedPath(Pick pick)
        {
            return pick.artPath.NullOrEmpty() ? item.graphicData.texPath : pick.artPath;
        }

        private List<Texture> PickedPictures(Pick pick)
        {
            return PickedOption(pick)?.Pictures ?? NormalPictures();
        }

        // The item's own art, which a look doesn't change; the type's icon follows the colony's age.
        private List<Texture> NormalPictures()
        {
            return StackPictures.Of(item.graphicData.Graphic);
        }

        private enum LookSource
        {
            Pick,
            Pack,
            Normal,
        }

        private readonly struct AgeLook
        {
            public AgeLook(List<Texture> pictures, string name, LookSource source, TechLevel rowAge)
            {
                Pictures = (pictures ?? new List<Texture>()).Where(picture => picture != null).ToList();
                Name = name ?? "";
                Source = source;
                RowAge = rowAge;
            }

            public List<Texture> Pictures { get; }
            public string Name { get; }
            public LookSource Source { get; }

            /// <summary>The age of the row the look comes from, later than the age shown when that row covers it.</summary>
            public TechLevel RowAge { get; }

            /// <summary>
            /// The item's name in the age, with where it comes from when that isn't the age's own pick, such as
            /// "Hole (same as Medieval)".
            /// </summary>
            public string NameLine(TechLevel age, bool coloured)
            {
                string name = Name.CapitalizeFirst();
                // Resolve keeps the colour tags, which turning a TaggedString into a string strips.
                if (Source == LookSource.Normal)
                {
                    return "VPAP_NameNormal".Translate(name).Resolve();
                }
                if (RowAge != age)
                {
                    string rowAge = coloured ? AgeColors.Coloured(RowAge) : RowAge.ToStringHuman().CapitalizeFirst();
                    return "VPAP_NameSameAs".Translate(name, rowAge).Resolve();
                }
                return Source == LookSource.Pack ? "VPAP_NameFromPack".Translate(name).Resolve() : name;
            }
        }
    }
}
