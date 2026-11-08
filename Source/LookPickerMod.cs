using HarmonyLib;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>Reads the player's picks and hooks the game's def loading, where they become looks.</summary>
    public class LookPickerMod : Mod
    {
        public static LookPickerSettings Settings { get; private set; }

        public static ModContentPack ContentPack { get; private set; }

        public LookPickerMod(ModContentPack content) : base(content)
        {
            ContentPack = content;
            Settings = GetSettings<LookPickerSettings>();
            new Harmony("groovy.visualprogression.lookpicker").PatchAll();
        }
    }
}
