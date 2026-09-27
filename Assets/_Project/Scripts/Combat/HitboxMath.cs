using System;

namespace Margin.Combat
{
    /// <summary>An axis-aligned box: center and size in units. Pure C# for testable hit detection.</summary>
    public readonly struct AabbBox
    {
        public readonly float CenterX, CenterY, Width, Height;

        public AabbBox(float centerX, float centerY, float width, float height)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = Math.Abs(width);
            Height = Math.Abs(height);
        }

        public float MinX => CenterX - Width / 2f;
        public float MaxX => CenterX + Width / 2f;
        public float MinY => CenterY - Height / 2f;
        public float MaxY => CenterY + Height / 2f;
    }

    public static class HitboxMath
    {
        /// <summary>
        /// Places a hitbox authored for a right-facing attacker into the world.
        /// facing = -1 mirrors the offset to the left.
        /// </summary>
        public static AabbBox ToWorld(float originX, float originY, int facing, float offsetX, float offsetY,
                                      float width, float height)
        {
            return new AabbBox(originX + offsetX * (facing < 0 ? -1f : 1f), originY + offsetY, width, height);
        }

        /// <summary>True if the boxes overlap. Boxes that only touch edges do not count.</summary>
        public static bool Overlaps(AabbBox a, AabbBox b)
        {
            return a.MinX < b.MaxX && a.MaxX > b.MinX && a.MinY < b.MaxY && a.MaxY > b.MinY;
        }

        /// <summary>Center of the overlapping area (where hit effects spawn). Only meaningful if they overlap.</summary>
        public static void OverlapCenter(AabbBox a, AabbBox b, out float x, out float y)
        {
            x = (Math.Max(a.MinX, b.MinX) + Math.Min(a.MaxX, b.MaxX)) / 2f;
            y = (Math.Max(a.MinY, b.MinY) + Math.Min(a.MaxY, b.MaxY)) / 2f;
        }
    }
}
