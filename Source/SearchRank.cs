using System;

namespace VisualProgression.AgePicker
{
    /// <summary>How well a name matches search text, so searches can list the best matches first.</summary>
    internal static class SearchRank
    {
        public const int WholeName = 0;
        public const int NameStart = 1;
        public const int WordStart = 2;
        public const int Anywhere = 3;
        public const int NotInName = 4;

        /// <summary>
        /// The rank of the name for the text, lower being better: the whole name, its start, the start of a word in it,
        /// anywhere in it, or not in it. Case-insensitive, as the game's own search is.
        /// </summary>
        public static int Of(string name, string text)
        {
            // Ordinal, as the art search matches, and fast enough to rank every picture on each keystroke.
            const StringComparison IgnoreCase = StringComparison.OrdinalIgnoreCase;
            if (string.Equals(name, text, IgnoreCase))
            {
                return WholeName;
            }
            int at = name.IndexOf(text, IgnoreCase);
            if (at < 0)
            {
                return NotInName;
            }
            if (at == 0)
            {
                return NameStart;
            }
            for (; at >= 0; at = name.IndexOf(text, at + 1, IgnoreCase))
            {
                if (StartsWord(name, at))
                {
                    return WordStart;
                }
            }
            return Anywhere;
        }

        // A word starts after a space, underscore or other separator, or at a capital after a lowercase letter ("BionicEar").
        private static bool StartsWord(string name, int at)
        {
            char before = name[at - 1];
            return !char.IsLetterOrDigit(before) || (char.IsUpper(name[at]) && char.IsLower(before));
        }
    }
}
