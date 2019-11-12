// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extensions methods fpor the <see cref="GraphicsOptions"/> class.
    /// </summary>
    internal static class GraphicsOptionsExtensions
    {
        /// <summary>
        /// Evaluates if a given SOURCE color can completely replace a BACKDROP color given the current blending and composition settings.
        /// </summary>
        /// <param name="options">The graphics options.</param>
        /// <param name="color">The source color.</param>
        /// <returns>true if the color can be considered opaque</returns>
        /// <remarks>
        /// Blending and composition is an expensive operation, in some cases, like
        /// filling with a solid color, the blending can be avoided by a plain color replacement.
        /// This method can be useful for such processors to select the fast path.
        /// </remarks>
        public static bool IsOpaqueColorWithoutBlending(this GraphicsOptions options, Color color)
        {
            if (options.ColorBlendingMode != PixelColorBlendingMode.Normal)
            {
                return false;
            }

            if (options.AlphaCompositionMode != PixelAlphaCompositionMode.SrcOver
                && options.AlphaCompositionMode != PixelAlphaCompositionMode.Src)
            {
                return false;
            }

            const float Opaque = 1F;

            if (options.BlendPercentage != Opaque)
            {
                return false;
            }

            if (((Vector4)color).W != Opaque)
            {
                return false;
            }

            return true;
        }
    }
}
