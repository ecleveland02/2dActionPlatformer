using System;

namespace Margin.Combat
{
    /// <summary>
    /// Hit points with post-hit invulnerability (spec 6.7), in pure C# for testing.
    /// Frame rule: the tick of the hit is frame 1 of the invulnerability window.
    /// </summary>
    public sealed class Health
    {
        public int Max { get; }
        public int Current { get; private set; }
        public int InvulnerableFramesLeft { get; private set; }
        public bool IsInvulnerable => InvulnerableFramesLeft > 0;
        public bool IsDepleted => Current <= 0;
        public float Fraction => Current / (float)Max;

        public Health(int max)
        {
            Max = Math.Max(1, max);
            Current = Max;
        }

        /// <summary>Takes damage and starts invulnerability. Ignored while invulnerable. Returns damage actually taken.</summary>
        public int TakeDamage(int amount, int invulnerableFrames)
        {
            if (IsInvulnerable || amount <= 0) return 0;
            int taken = Math.Min(Current, amount);
            Current -= taken;
            InvulnerableFramesLeft = Math.Max(0, invulnerableFrames);
            return taken;
        }

        /// <summary>
        /// Starts (or extends) invulnerability without taking damage. The player gets it when a combo ends,
        /// so enemies can combo them but can't restart a new combo right away.
        /// </summary>
        public void StartInvulnerability(int frames) =>
            InvulnerableFramesLeft = Math.Max(InvulnerableFramesLeft, Math.Max(0, frames));

        /// <summary>Restores health, clamped to Max. Returns the amount actually healed.</summary>
        public int Heal(int amount)
        {
            int healed = Math.Min(Max - Current, Math.Max(0, amount));
            Current += healed;
            return healed;
        }

        /// <summary>Heals a fraction of Max (spec 6.6: Redraw restores 30%), rounded to the nearest point.</summary>
        public int HealFraction(float fraction) => Heal((int)Math.Round(Max * fraction));

        /// <summary>Call once per tick.</summary>
        public void Tick()
        {
            if (InvulnerableFramesLeft > 0) InvulnerableFramesLeft--;
        }

        public void Refill()
        {
            Current = Max;
            InvulnerableFramesLeft = 0;
        }
    }
}
