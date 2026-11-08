using System.Collections.Generic;
using System.Linq;

namespace VisualProgression.LookPicker
{
    /// <summary>Combines an item's looks from packs with the player's picks.</summary>
    public static class LookRows
    {
        /// <summary>
        /// The rows an item ends up with: the packs' rows, with each picked age's rows replaced by the pick, or the pick
        /// added when no pack has that age. When two picks share an age, the later one wins. Ordered by age. Neither
        /// list is changed.
        /// </summary>
        public static List<AgeRow> WithPicks(IReadOnlyList<AgeRow> packRows, IReadOnlyList<AgeRow> picks)
        {
            var lastPickByAge = new Dictionary<int, AgeRow>();
            foreach (AgeRow pick in picks)
            {
                lastPickByAge[pick.UpToAge] = pick;
            }
            List<AgeRow> rows = packRows.Where(row => !lastPickByAge.ContainsKey(row.UpToAge)).ToList();
            rows.AddRange(lastPickByAge.Values);
            // OrderBy is stable, so pack rows that share an age keep their order.
            return rows.OrderBy(row => row.UpToAge).ToList();
        }
    }
}
