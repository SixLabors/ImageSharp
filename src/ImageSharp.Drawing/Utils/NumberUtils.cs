// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Utility methods for numeric primitives.
    /// </summary>
    internal static class NumberUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ClampFloat(float value, float min, float max)
        {
            if (value >= max)
            {
                return max;
            }

            if (value <= min)
            {
                return min;
            }

            return value;
        }
    }
}
