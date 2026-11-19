using System;

namespace Sector.Ops
{
    // The five indices the city is judged by. Each runs 0..100.
    public enum Index
    {
        Happiness,     // 행복 지수
        Stability,     // 사회 안정 지수
        Cohesion,      // 사회 정합 지수: how well behavior, roles and wants fit the AI's design
        AutonomyFelt,  // 자율성 지수(인지): how freely people believe they choose
        AutonomyReal,  // 자율성 지수(실질): share of choices made without interference
    }

    public static class IndexNames
    {
        public static readonly Index[] All =
        {
            Index.Happiness, Index.Stability, Index.Cohesion, Index.AutonomyFelt, Index.AutonomyReal,
        };

        public static string Name(Index index)
        {
            switch (index)
            {
                case Index.Happiness: return "행복 지수";
                case Index.Stability: return "사회 안정 지수";
                case Index.Cohesion: return "사회 정합 지수";
                case Index.AutonomyFelt: return "자율성 지수(인지)";
                default: return "자율성 지수(실질)";
            }
        }

        public static string Short(Index index)
        {
            switch (index)
            {
                case Index.Happiness: return "행복";
                case Index.Stability: return "안정";
                case Index.Cohesion: return "정합";
                case Index.AutonomyFelt: return "자율(인지)";
                default: return "자율(실질)";
            }
        }
    }

    // A set of changes to the five indices.
    public struct Delta
    {
        public float Happiness, Stability, Cohesion, AutonomyFelt, AutonomyReal;

        public Delta(float happiness = 0f, float stability = 0f, float cohesion = 0f, float felt = 0f, float real = 0f)
        {
            Happiness = happiness;
            Stability = stability;
            Cohesion = cohesion;
            AutonomyFelt = felt;
            AutonomyReal = real;
        }

        public float this[Index index]
        {
            get
            {
                switch (index)
                {
                    case Index.Happiness: return Happiness;
                    case Index.Stability: return Stability;
                    case Index.Cohesion: return Cohesion;
                    case Index.AutonomyFelt: return AutonomyFelt;
                    default: return AutonomyReal;
                }
            }
            set
            {
                switch (index)
                {
                    case Index.Happiness: Happiness = value; break;
                    case Index.Stability: Stability = value; break;
                    case Index.Cohesion: Cohesion = value; break;
                    case Index.AutonomyFelt: AutonomyFelt = value; break;
                    default: AutonomyReal = value; break;
                }
            }
        }

        public static Delta operator *(Delta d, float k) =>
            new Delta(d.Happiness * k, d.Stability * k, d.Cohesion * k, d.AutonomyFelt * k, d.AutonomyReal * k);

        public static Delta operator +(Delta a, Delta b) =>
            new Delta(a.Happiness + b.Happiness, a.Stability + b.Stability, a.Cohesion + b.Cohesion,
                a.AutonomyFelt + b.AutonomyFelt, a.AutonomyReal + b.AutonomyReal);

        public bool IsZero
        {
            get
            {
                foreach (var index in IndexNames.All)
                    if (Math.Abs(this[index]) > 0.0001f)
                        return false;
                return true;
            }
        }

        // "행복 +2, 자율(인지) -3" for tooltips and logs.
        public string Describe(float scale = 1f)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var index in IndexNames.All)
            {
                float v = this[index] * scale;
                if (Math.Abs(v) < 0.05f)
                    continue;
                parts.Add($"{IndexNames.Short(index)} {(v > 0 ? "+" : "")}{v:0.#}");
            }
            return string.Join(", ", parts);
        }
    }

    public sealed class Indices
    {
        readonly float[] values = new float[5];

        public float this[Index index]
        {
            get => values[(int)index];
            set => values[(int)index] = Math.Max(0f, Math.Min(100f, value));
        }

        public void Apply(Delta delta)
        {
            foreach (var index in IndexNames.All)
                this[index] += delta[index];
        }

        public float Sum
        {
            get
            {
                float sum = 0f;
                foreach (var v in values)
                    sum += v;
                return sum;
            }
        }

        public float Average => Sum / values.Length;

        public Indices Copy()
        {
            var copy = new Indices();
            foreach (var index in IndexNames.All)
                copy[index] = this[index];
            return copy;
        }
    }
}
