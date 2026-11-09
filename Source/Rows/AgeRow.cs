namespace VisualProgression.AgePicker
{
    /// <summary>One of an item's looks: the look it's made with while the colony's age is at or below UpToAge.</summary>
    public readonly struct AgeRow
    {
        public AgeRow(int upToAge, string look)
        {
            UpToAge = upToAge;
            Look = look;
        }

        /// <summary>The highest age the look covers, as a tech level number.</summary>
        public int UpToAge { get; }

        /// <summary>The look's defName.</summary>
        public string Look { get; }
    }
}
