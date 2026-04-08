namespace FSlice.Gameplay
{
    /// <summary>
    /// Immutable data for a single note event parsed from a chart.
    /// </summary>
    public readonly struct NoteData
    {
        /// <summary>Time in seconds from song start when this note should be hit.</summary>
        public readonly double Time;

        /// <summary>0 = Left, 1 = Down, 2 = Up, 3 = Right</summary>
        public readonly int Direction;

        /// <summary>Hold duration in seconds. 0 means tap note.</summary>
        public readonly double Length;

        /// <summary>True = player must hit this note. False = opponent lane (auto-played).</summary>
        public readonly bool MustHit;

        public NoteData(double time, int direction, double length = 0.0, bool mustHit = true)
        {
            Time     = time;
            Direction = direction;
            Length   = length;
            MustHit  = mustHit;
        }

        public bool IsHold => Length > 0.0;

        public override string ToString() =>
            $"[{DirectionName} @ {Time:F3}s{(IsHold ? $" hold {Length:F3}s" : "")}]";

        public static string DirectionName(int dir) => dir switch
        {
            0 => "Left",
            1 => "Down",
            2 => "Up",
            3 => "Right",
            _ => "???"
        };

        public string DirectionName => DirectionName(Direction);
    }
}
