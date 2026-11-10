using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>
    /// An item's symbol: a mask from a file of its own that a mod draws over each of its pictures, as Bionic icons draws a
    /// body part over the box for its kind. Art picked from the mod that holds the file keeps the symbol, with the shader
    /// and colours it's drawn with. A mask shaped for one picture would be wrong on another mod's picture, so art from any
    /// other mod doesn't take it.
    /// </summary>
    internal static class ItemSymbol
    {
        private static readonly AccessTools.FieldRef<ThingStyleDef, Graphic> styleGraphic =
            AccessTools.FieldRefAccess<ThingStyleDef, Graphic>("graphic");
        private static readonly AccessTools.FieldRef<GraphicData, Graphic> cachedGraphic =
            AccessTools.FieldRefAccess<GraphicData, Graphic>("cachedGraphic");
        private static readonly Action<ThingStyleDef> resolveUIIcon =
            AccessTools.MethodDelegate<Action<ThingStyleDef>>(AccessTools.Method(typeof(ThingStyleDef), "ResolveUIIcon"));

        // Looks made from picked art, given their symbol once every mod's startup code has run.
        private static readonly List<(ThingDef item, ThingStyleDef look, string artMod)> waiting =
            new List<(ThingDef, ThingStyleDef, string)>();
        private static readonly Dictionary<(ThingDef, ModContentPack), bool> heldByMod = new Dictionary<(ThingDef, ModContentPack), bool>();

        /// <summary>Gives the look the item's symbol once the item's art is final, if its art mod holds the symbol.</summary>
        public static void KeepLater(ThingDef item, ThingStyleDef look, string artMod)
        {
            waiting.Add((item, look, artMod));
        }

        /// <summary>
        /// Gives each waiting look the item's symbol. Mods like Bionic icons set an item's symbol in their startup code,
        /// after the looks are made, so this runs once every mod's startup code has. It names no framework types, so a
        /// missing or outdated framework fails inside the try.
        /// </summary>
        public static void KeepAll()
        {
            try
            {
                KeepWaiting();
            }
            catch (Exception e)
            {
                Log.Warning("[Age Picker] Couldn't give picked art the item's symbol, so it shows without it: " + e);
            }
        }

        /// <summary>Whether art from the mod takes the item's symbol.</summary>
        public static bool Applies(ThingDef item, ModContentPack artMod)
        {
            Graphic symbol = SymbolGraphicOf(item);
            if (symbol == null || artMod == null)
            {
                return false;
            }
            if (!heldByMod.TryGetValue((item, artMod), out bool held))
            {
                held = ArtCatalog.TextureFrom(artMod, symbol.maskPath) != null;
                heldByMod[(item, artMod)] = held;
            }
            return held;
        }

        /// <summary>
        /// The material to show the picture with in the picker: the item's symbol over it, drawn as an icon is. Null when
        /// the art doesn't take the item's symbol. A null mod is the item's own art.
        /// </summary>
        public static Material PreviewMaterial(ThingDef item, ModContentPack artMod, Texture picture)
        {
            if (picture == null || !(artMod == null ? SymbolGraphicOf(item) != null : Applies(item, artMod)))
            {
                return null;
            }
            Material itemMaterial = SymbolGraphicOf(item).MatSingle;
            if (!ShaderDatabase.TryGetUIShader(itemMaterial.shader, out Shader uiShader) ||
                !MaterialPool.TryGetRequestForMat(itemMaterial, out MaterialRequest request))
            {
                return null;
            }
            request.mainTex = picture;
            request.shader = uiShader;
            return MaterialPool.MatFrom(request);
        }

        private static void KeepWaiting()
        {
            foreach ((ThingDef item, ThingStyleDef look, string artMod) in waiting)
            {
                ModContentPack mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(running =>
                    string.Equals(running.PackageIdPlayerFacing, artMod, StringComparison.OrdinalIgnoreCase));
                if (look.graphicData != null && Applies(item, mod))
                {
                    KeepOn(item, look);
                }
            }
            waiting.Clear();
        }

        // Rebuilds the look's graphic and icon with the symbol, as ThingStyleDef.PostLoad builds them.
        private static void KeepOn(ThingDef item, ThingStyleDef look)
        {
            Graphic symbol = SymbolGraphicOf(item);
            ShaderTypeDef shaderType = DefDatabase<ShaderTypeDef>.AllDefsListForReading.FirstOrDefault(def => def.Shader == symbol.Shader);
            if (shaderType == null)
            {
                return;
            }
            GraphicData art = look.graphicData;
            art.shaderType = shaderType;
            art.maskPath = symbol.maskPath;
            art.color = symbol.color;
            art.colorTwo = symbol.colorTwo;
            cachedGraphic(art) = null;
            styleGraphic(look) = art.Graphic;
            resolveUIIcon(look);
        }

        // The item's single picture graphic when it draws a symbol from a file of its own, or null.
        private static Graphic SymbolGraphicOf(ThingDef item)
        {
            return item?.graphic is Graphic_Single graphic && !graphic.maskPath.NullOrEmpty() && graphic.Shader.SupportsMaskTex()
                ? graphic
                : null;
        }
    }

    /// <summary>Gives picked art the item's symbol after every mod's startup code, whatever the load order.</summary>
    [StaticConstructorOnStartup]
    internal static class ItemSymbolsAtStartup
    {
        static ItemSymbolsAtStartup()
        {
            // The game runs every startup constructor from this queue, and runs what's added to it afterwards.
            LongEventHandler.ExecuteWhenFinished(ItemSymbol.KeepAll);
        }
    }
}
