namespace Sector.Ops
{
    // A small random generator whose whole state is one number, so saves can restore it exactly.
    public sealed class Rng
    {
        public ulong State;

        public Rng(ulong seed)
        {
            State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong Next()
        {
            // xorshift64*
            State ^= State >> 12;
            State ^= State << 25;
            State ^= State >> 27;
            return State * 0x2545F4914F6CDD1DUL;
        }

        // 0 <= x < 1
        public float Value() => (Next() >> 40) / (float)(1UL << 24);

        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (ulong)(max - min));

        public bool Chance(float p) => Value() < p;
    }
}
