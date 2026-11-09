using RimWorld;
using Verse;

namespace VisualProgression.LookPicker
{
    /// <summary>
    /// A player's look for one item up to one age: art from a chosen mod, a name, or both. Like a pack's row, it covers
    /// that age and the earlier ones down to the item's next row.
    /// </summary>
    public class Pick : IExposable
    {
        /// <summary>The item's defName.</summary>
        public string item;

        public TechLevel upToAge;

        /// <summary>The packageId of the mod whose art the look uses. Empty keeps the item's current art.</summary>
        public string artMod;

        /// <summary>The texture path in that mod. Empty uses the item's own path.</summary>
        public string artPath;

        /// <summary>The item's name at those ages. Empty keeps its name.</summary>
        public string name;

        /// <summary>True when the art is one picture on an item whose art is a folder, so it shows at every stack size.</summary>
        public bool singlePicture;

        /// <summary>True when the age uses the item's normal art even where a pack's look would cover it.</summary>
        public bool normalArt;

        public bool HasArt => !artMod.NullOrEmpty();

        public bool HasName => !name.NullOrEmpty();

        /// <summary>True when the pick changes nothing: no art, no name, and not asking for the normal art.</summary>
        public bool IsEmpty => !HasArt && !HasName && !normalArt;

        public void ExposeData()
        {
            Scribe_Values.Look(ref item, "item");
            Scribe_Values.Look(ref upToAge, "upToAge");
            Scribe_Values.Look(ref artMod, "artMod");
            Scribe_Values.Look(ref artPath, "artPath");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref singlePicture, "singlePicture");
            Scribe_Values.Look(ref normalArt, "normalArt");
        }
    }
}
