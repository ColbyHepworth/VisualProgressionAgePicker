using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// Turns the player's picks into looks while the game makes its own generated defs, so the item's ResolveReferences,
    /// the def error check and short hashes all come after, as for a pack's looks. Made in code rather than as XML, since
    /// XML caches like Missile Girl's skip patches when mod files haven't changed.
    /// </summary>
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class PickedLooksHook
    {
        // An error here would make the game reset the player's mod list, so nothing may escape. This method names no
        // framework types, so a missing or outdated framework fails inside the try.
        private static void Postfix(bool hotReload)
        {
            try
            {
                PickedLooks.MakeAll(hotReload);
            }
            catch (Exception e)
            {
                Log.Warning("[Age Picker] Couldn't make the picked looks, so items keep their normal looks. " +
                    "Is Visual Progression Framework active and up to date? " + e);
            }
        }
    }
}
