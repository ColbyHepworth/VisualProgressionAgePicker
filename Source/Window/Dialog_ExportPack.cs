using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// Exports the player's picks as a pack in the game's Mods folder, then says where it went, which mods it needs and
    /// which picks it left out.
    /// </summary>
    public class Dialog_ExportPack : Window
    {
        private const float TitleHeight = 40f;
        private const float RowHeight = 30f;
        private const float LabelWidth = 110f;

        private string packName = "";
        private string author;
        private string result;
        private Vector2 resultScroll;

        public Dialog_ExportPack()
        {
            string steamName = SteamUtility.SteamPersonaName;
            author = steamName == "???" ? "" : steamName;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(620f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 30f, TitleHeight), "VPAP_ExportTitle".Translate());
            Text.Font = GameFont.Small;
            float y = inRect.y + TitleHeight;

            packName = TextRow(new Rect(inRect.x, y, inRect.width, RowHeight), "VPAP_PackName", packName);
            y += RowHeight + 6f;
            author = TextRow(new Rect(inRect.x, y, inRect.width, RowHeight), "VPAP_Author", author);
            y += RowHeight + 6f;

            bool ready = !packName.Trim().NullOrEmpty() && !author.Trim().NullOrEmpty();
            GUI.color = ColorLibrary.Grey;
            string folderNote = ready
                ? "VPAP_WritesTo".Translate(Path.Combine(GenFilePaths.ModsFolderPath, GenFile.SanitizedFileName(packName.Trim())))
                : "VPAP_NameTheExport".Translate();
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 44f), folderNote);
            GUI.color = Color.white;
            y += 48f;

            if (Widgets.ButtonText(new Rect(inRect.x, y, 160f, RowHeight), "VPAP_Export".Translate(), active: ready) && ready)
            {
                Export();
            }
            y += RowHeight + 12f;

            if (result != null)
            {
                var resultRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
                Widgets.LabelScrollable(resultRect, result, ref resultScroll);
            }
        }

        private static string TextRow(Rect rect, string labelKey, string text)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, LabelWidth, rect.height), labelKey.Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            return Widgets.TextField(new Rect(rect.x + LabelWidth, rect.y, rect.width - LabelWidth, rect.height), text);
        }

        private void Export()
        {
            ExportedPack pack;
            string folder;
            string problem;
            try
            {
                pack = PackExport.Build(packName, author, AgePickerMod.Settings.picks);
                folder = PackExport.FolderFor(pack);
                problem = PackExport.FolderProblem(pack);
            }
            catch (Exception e)
            {
                Fail("VPAP_ExportFailed".Translate(packName, e.Message));
                return;
            }
            if (problem != null)
            {
                Fail(problem);
                return;
            }
            if (Directory.Exists(folder))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "VPAP_ReplaceExport".Translate(pack.folderName), () => Write(pack, folder), destructive: true));
                return;
            }
            Write(pack, folder);
        }

        private void Write(ExportedPack pack, string folder)
        {
            try
            {
                PackExport.WriteTo(pack, folder);
            }
            catch (Exception e)
            {
                Fail("VPAP_ExportFailed".Translate(folder, e.Message));
                return;
            }
            Messages.Message("VPAP_Exported".Translate(pack.folderName), MessageTypeDefOf.TaskCompletion, historical: false);
            var lines = new List<string> { "VPAP_ExportDone".Translate(folder) };
            IEnumerable<string> needs = pack.dependencies.Select(id => ModLister.GetModWithIdentifier(id, ignorePostfix: true)?.Name ?? id);
            lines.Add("VPAP_ExportNeeds".Translate(string.Join(", ", needs)));
            if (pack.leftOut.Count > 0)
            {
                lines.Add("VPAP_ExportLeftOut".Translate() + "\n" + string.Join("\n", pack.leftOut.Select(line => "  - " + line)));
            }
            lines.Add("VPAP_ExportAfter".Translate());
            result = string.Join("\n\n", lines);
        }

        private void Fail(string reason)
        {
            result = reason;
            Messages.Message(reason, MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
