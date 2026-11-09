using HarmonyLib;
using UnityEngine;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>Reads the player's picks, hooks the game's def loading where they become looks, and lists them in settings.</summary>
    public class LookPickerMod : Mod
    {
        private readonly ChangedItemsList changedItems = new ChangedItemsList();

        public LookPickerMod(ModContentPack content) : base(content)
        {
            ContentPack = content;
            Settings = GetSettings<LookPickerSettings>();
            new Harmony("groovy.visualprogression.lookpicker").PatchAll();
        }

        public static LookPickerSettings Settings { get; private set; }

        public static ModContentPack ContentPack { get; private set; }

        /// <summary>True once the picks were changed in this session, so the looks wait for a restart.</summary>
        public static bool ChangedSinceLaunch { get; set; }

        public override string SettingsCategory()
        {
            return Content.Name;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            changedItems.Draw(inRect);
        }
    }
}
