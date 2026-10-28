// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Normalizes PGM and PPM samples without rounding them to an intermediate integer format.
/// </summary>
internal static class PbmSampleScaler
{
    /// <summary>
    /// Normalizes four encoded component magnitudes to [0, 1]. Values outside the declared range saturate at its endpoints.
    /// </summary>
    /// <param name="value">The RGB sample magnitudes and an alpha magnitude equal to Maxval.</param>
    /// <param name="maxPixelValue">The maxval declared in the header, between 1 and 65535.</param>
    /// <returns>Normalized pixel components with opaque alpha.</returns>
    public static Vector4 Scale(Vector4 value, int maxPixelValue)
    {
        // Maxval is a validated positive integer. Division maps Maxval to exactly one, including the alpha lane.
        // Saturation is a numeric conversion rule and does not depend on SegmentIntegrityHandling.
        value = ColorNumerics.ClampToFinite(value, Vector4.Zero, new Vector4(maxPixelValue));
        return value / maxPixelValue;
    }
}
