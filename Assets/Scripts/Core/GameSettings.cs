namespace PhantomChaseVR
{
    /// <summary>
    /// Shared game constants referenced by all domains.
    /// Do not redefine these values elsewhere.
    /// </summary>
    public static class GameSettings
    {
        public const int TOTAL_NODES = 62;
        public const float NODE_SPACING_X = 30f;
        public const float NODE_SPACING_Z = 30f;

        public static readonly int[] UNDERGROUND_A_NODES = { 11, 44, 55 };
        public static readonly int[] UNDERGROUND_B_NODES = { 17, 50 };
        public static readonly int[] CROWD_BLEND_NODES = { 36, 38, 47, 49, 25 };
        public const int TIMES_SQUARE_NODE = 25;
    }
}
