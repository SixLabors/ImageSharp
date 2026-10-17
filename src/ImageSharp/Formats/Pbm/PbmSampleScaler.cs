// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Scales PGM and PPM samples from the range declared by the header maxval to the full range of the component type.
/// </summary>
internal static class PbmSampleScaler
{
    /// <summary>
    /// Scales a sample from the range [0, <paramref name="maxPixelValue"/>] to the range [0, <paramref name="targetMaxValue"/>],
    /// rounding to the nearest value. Samples outside the declared range are clamped to <paramref name="maxPixelValue"/>.
    /// </summary>
    /// <param name="value">The sample value read from the stream.</param>
    /// <param name="maxPixelValue">The maxval declared in the header, between 1 and 65535.</param>
    /// <param name="targetMaxValue">The maximum value of the component type, either 255 or 65535.</param>
    /// <returns>The scaled sample value.</returns>
    public static int Scale(int value, int maxPixelValue, int targetMaxValue)
    {
        // Negative values can only come from an overflowing decimal sample; reinterpreting them as unsigned
        // makes them clamp to maxval along with every other out-of-range sample.
        uint sample = Math.Min((uint)value, (uint)maxPixelValue);

        // Adding half of the divisor before dividing rounds to the nearest value, so maxval maps exactly to
        // targetMaxValue and the scale does not depend on maxval dividing the target range.
        // The largest intermediate value is 65535 * 65535 + 32767, which fits in an unsigned 32-bit integer.
        return (int)(((sample * (uint)targetMaxValue) + ((uint)maxPixelValue >> 1)) / (uint)maxPixelValue);
    }
}
