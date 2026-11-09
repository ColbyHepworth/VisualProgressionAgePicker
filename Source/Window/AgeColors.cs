using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VisualProgression.AgePicker
{
    /// <summary>A colour for each tech age, so ages are told apart at a glance.</summary>
    public static class AgeColors
    {
        // Light enough to read on the game's dark windows.
        private static readonly Dictionary<TechLevel, string> HexByAge = new Dictionary<TechLevel, string>
        {
            { TechLevel.Animal, "#c79a76" },
            { TechLevel.Neolithic, "#94c46c" },
            { TechLevel.Medieval, "#d3b25e" },
            { TechLevel.Industrial, "#8fb5d8" },
            { TechLevel.Spacer, "#6fcfc8" },
            { TechLevel.Ultra, "#e48cc3" },
            { TechLevel.Archotech, "#e5c45a" },
        };

        /// <summary>The age's colour, or white for an age without one.</summary>
        public static Color For(TechLevel age)
        {
            return HexByAge.TryGetValue(age, out string hex) ? GenColor.FromHex(hex) : Color.white;
        }

        /// <summary>The age's name in its colour, as rich text for a label.</summary>
        public static string Coloured(TechLevel age)
        {
            return ColoredText.Colorize(age.ToStringHuman().CapitalizeFirst(), For(age));
        }
    }
}
