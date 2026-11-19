namespace Sector.Ops
{
    // Every number the prototype is balanced with, in one place.
    public static class OpsTuning
    {
        // Era 1 runs 2041-01-01 .. 2045-12-31.
        public const int StartYear = 2041;
        public const int Years = 5;

        // Starting city: unhappy, unsettled, loosely fitted, very free.
        public static readonly Delta Start = new Delta(happiness: 35f, stability: 40f, cohesion: 25f, felt: 85f, real: 90f);

        // Each week every index drifts this fraction of the way back to its resting level.
        public static readonly Delta Rest = new Delta(happiness: 35f, stability: 40f, cohesion: 20f, felt: 85f, real: 90f);
        public static readonly Delta RestPull = new Delta(happiness: 0.05f, stability: 0f, cohesion: 0.03f, felt: 0.03f, real: 0.005f);

        // Weekly stability pressure: unhappiness wears it down.
        public const float GrumbleBase = 0.8f;
        public const float GrumbleHappinessPoint = 50f;
        public const float GrumblePerHappiness = 0.06f;

        // Energy.
        public const float StartEnergy = 100f;
        public const float StartEnergyCap = 150f;
        public const float BasePower = 8f;

        // Info: weekly income at an average index of 100.
        public const float InfoPerWeek = 4f;
        public const float InfoFirstEvent = 6f;
        public const float InfoRepeatEvent = 2f;
        public const float InfoFirstProfile = 4f;

        // Policy and facility effects shrink near the ends of the scale: gains slow above 60, losses below 40.
        public const float SoftEdge = 40f;

        // Event answers are this strong relative to the numbers written on them.
        public const float EventScale = 0.5f;

        // Unrest events: daily chance.
        public const float EventBase = 0.03f;
        public const float EventPerStability = 0.002f;   // per point under 60
        public const float EventPerHappiness = 0.0015f;  // per point under 60
        public const float EventMin = 0.01f;
        public const float EventMax = 0.2f;
        // Events cannot fire on consecutive days.
        public const int EventCooldownDays = 3;

        // Policy slots at the start; delegation adds more.
        public const int StartSlots = 3;
        public const int FacilitySlotsPerZone = 2;

        // Delegation: the visible index sum (all but real autonomy) needed to rise from each stage
        // (index = stage - 1), held for StreakMonths. It starts near 185.
        public static readonly float[] RiseAt = { 235f, 272f, 300f, 318f, 328f, 336f };
        public const int StreakMonths = 4;
        // Stability under this for FallMonths months in a row drops a stage.
        public const float FallStability = 25f;
        public const int FallMonths = 3;
        public const int MaxStage = 7;

        // Seen (control) policies wear off: effect -HabitPerWeek per week active, never below HabitFloor.
        public const float HabitPerWeek = 0.012f;
        public const float HabitFloor = 0.55f;

        // Groups: each week satisfaction moves toward happiness by GroupPull, and happiness toward the
        // population-weighted satisfaction by HappinessFromGroups. A group under GroupUnrestPoint stirs unrest.
        public const float GroupPull = 0.06f;
        public const float HappinessFromGroups = 0.04f;
        public const float GroupUnrestPoint = 35f;
        public const float GroupUnrestPerPoint = 0.0015f;
        // Facilities lift every group by this share of their weekly happiness effect.
        public const float FacilityGroupShare = 1f;
        // Weekly population change per point of zone satisfaction above 50.
        public const float PopulationGrowth = 0.0002f;

        // Base observed residents besides the fixed cast, per zone.
        public const int SamplesPerZone = 3;
    }
}
