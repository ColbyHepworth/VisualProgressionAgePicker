using System.Collections.Generic;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>The player's picks, kept in the mod's settings file. They take effect on the next start.</summary>
    public class AgePickerSettings : ModSettings
    {
        /// <summary>The settings file's format, so a later version can read older files.</summary>
        public int version = 1;

        public List<Pick> picks = new List<Pick>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref version, "version", 1);
            Scribe_Collections.Look(ref picks, "picks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && picks == null)
            {
                picks = new List<Pick>();
            }
        }
    }
}
