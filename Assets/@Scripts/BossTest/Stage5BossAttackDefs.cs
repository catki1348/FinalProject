// Stage5BossAttackDefs.cs - Stage 5 boss (Twin-Blade Demon Overlord) attack / combo data for the Unity TEST scene.
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15.
// This is concept/preview test data (rule 51), not applied to the main game.
// Numbers come from @Edit/BossDesign_Stage5_TwinBladeDemonOverlord.md (design v3: five attacks) and the v2 animation timing
// in @Edit/Stage5_Attacks_v2/build_v2.py. Units: world units, facing right, origin = boss feet.
namespace Stage5BossTest
{
    // Mid = body-height attack (avoid by moving away), Low = ground-height attack (avoid by jumping).
    public enum HitHeight { Mid, Low }

    // One damaging window of an attack: which animation frame opens it and the box it covers.
    public sealed class HitDef
    {
        public readonly int FrameIndex;   // frame (0-based) during which the window is open
        public readonly float XMin, XMax; // box extent in front of the boss (or around the target / both sides)
        public float YMin, YMax;          // box height range above the floor
        public readonly int Damage;
        public readonly HitHeight Height;
        public readonly bool Symmetric;   // true: box spans both sides of the boss (-XMax..+XMax)
        public readonly bool AtTarget;    // true: box is centered on the position locked at attack start

        public HitDef(int frame, float xMin, float xMax, HitHeight height, int damage, bool symmetric = false, bool atTarget = false)
        {
            FrameIndex = frame; XMin = xMin; XMax = xMax; Height = height; Damage = damage; Symmetric = symmetric; AtTarget = atTarget;
            YMin = height == HitHeight.Mid ? 0.6f : 0f;   // design section 3: mid 0.6~2.4, low 0~0.6
            YMax = height == HitHeight.Mid ? 2.4f : 0.6f;
        }
    }

    // One attack: sprite frame names, hold seconds per frame, the impact frame, forward lunge offsets (px) and hit windows.
    public sealed class AttackDef
    {
        public readonly string Id, Label;
        public readonly HitHeight Category;
        public readonly bool IsSpecial;
        public readonly string[] Frames;
        public readonly float[] Holds;
        public readonly int ImpactIndex;
        public readonly float[] OffsetsPx;
        public readonly int WarnFrom;      // danger-zone preview starts at this frame
        public readonly HitDef[] Hits;
        public readonly bool TravelToTarget; // boss moves to the locked target position before impact (unused now)
        public readonly int ChainSkip;       // frames skipped when this attack is chained (anticipation already shown by the previous attack)

        public AttackDef(string id, string label, HitHeight category, bool special, string[] frames, float[] holds,
                         int impact, float[] offsetsPx, int warnFrom, HitDef[] hits, bool travel = false, int chainSkip = 0)
        {
            Id = id; Label = label; Category = category; IsSpecial = special; Frames = frames; Holds = holds;
            ImpactIndex = impact; OffsetsPx = offsetsPx; WarnFrom = warnFrom; Hits = hits; TravelToTarget = travel; ChainSkip = chainSkip;
        }
    }

    // A mixed-attack pattern: a list of attack ids played back to back (design section 5).
    public sealed class ComboDef
    {
        public readonly string Label;
        public readonly string[] Ids;
        public readonly bool HasSpecial;
        public ComboDef(string label, bool hasSpecial, params string[] ids) { Label = label; HasSpecial = hasSpecial; Ids = ids; }
    }

    public static class Stage5BossAttackDefs
    {
        static HitDef[] H(params HitDef[] hits) { return hits; }

        public static readonly AttackDef N1 = new AttackDef("N1", "N1 RightSlash (Mid)", HitHeight.Mid, false,
            new[] { "N1_1", "N1_2", "N1_3", "N1_4" }, new[] { .11f, .15f, .08f, .15f }, 2, new float[] { -8, -22, 34, 52 }, 1,
            H(new HitDef(2, 0.4f, 3.4f, HitHeight.Mid, 14)));
        public static readonly AttackDef N2 = new AttackDef("N2", "N2 GroundSweep (Low)", HitHeight.Low, false,
            new[] { "N2_1", "N2_2", "N2_3", "N2_4" }, new[] { .12f, .16f, .07f, .17f }, 3, new float[] { -6, -20, 20, 48 }, 1,
            H(new HitDef(3, 0.4f, 4.0f, HitHeight.Low, 12)));
        public static readonly AttackDef N3 = new AttackDef("N3", "N3 ThrustLunge (Mid)", HitHeight.Mid, false,
            new[] { "N3_1", "N3_2", "N3_3", "N3_4" }, new[] { .11f, .16f, .07f, .17f }, 3, new float[] { -8, -24, 26, 56 }, 1,
            H(new HitDef(3, 0.4f, 4.4f, HitHeight.Mid, 16)));
        // Chain attacks "특공 4+5": 40 damage each (80 if both hit). Chained, they start at frame 1 (the windup pose) because
        // the basic attack before them already telegraphs the chain.
        public static readonly AttackDef N4 = new AttackDef("N4", "N4 CrossSlash (Mid, 40)", HitHeight.Mid, true,
            new[] { "N4_1", "N4_2", "N4_3", "N4_4" }, new[] { .10f, .15f, .07f, .17f }, 3, new float[] { -8, 0, 22, 46 }, 1,
            H(new HitDef(3, 0.4f, 3.6f, HitHeight.Mid, 40)), false, 1);
        public static readonly AttackDef N5 = new AttackDef("N5", "N5 SpinLowSlash (Low, 40)", HitHeight.Low, true,
            new[] { "N5_1", "N5_2", "N5_3", "N5_4" }, new[] { .11f, .16f, .07f, .18f }, 3, new float[] { -6, 10, 28, 40 }, 1,
            H(new HitDef(3, 0f, 3.2f, HitHeight.Low, 40, true)), false, 1);

        public static readonly AttackDef[] All = { N1, N2, N3, N4, N5 };

        // Design v3: basic attack alone, or basic attack + chain 4 + 5. N4/N5 are never used alone.
        public static readonly ComboDef[] Combos =
        {
            new ComboDef("P1 N1", false, "N1"),
            new ComboDef("P2 N2", false, "N2"),
            new ComboDef("P3 N3", false, "N3"),
            new ComboDef("P4 N1+N4+N5", true, "N1", "N4", "N5"),
            new ComboDef("P5 N2+N4+N5", true, "N2", "N4", "N5"),
            new ComboDef("P6 N3+N4+N5", true, "N3", "N4", "N5"),
        };

        // Finds an attack by id ("N1".."N5"). Linear search over 5 items; only called when an attack starts.
        public static AttackDef Get(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
