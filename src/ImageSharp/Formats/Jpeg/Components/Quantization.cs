// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// Provides methods and properties related to jpeg quantization.
/// </summary>
internal static class Quantization
{
    /// <summary>
    /// Upper bound (inclusive) for jpeg quality setting.
    /// </summary>
    public const int MaxQualityFactor = 100;

    /// <summary>
    /// Lower bound (inclusive) for jpeg quality setting.
    /// </summary>
    public const int MinQualityFactor = 1;

    /// <summary>
    /// Default JPEG quality for both luminance and chominance tables.
    /// </summary>
    public const int DefaultQualityFactor = 75;

    /// <summary>
    /// Represents lowest quality setting which can be estimated with enough confidence.
    /// Any quality below it results in a highly compressed jpeg image
    /// which shouldn't use standard itu quantization tables for re-encoding.
    /// </summary>
    public const int QualityEstimationConfidenceLowerThreshold = 25;

    /// <summary>
    /// Represents highest quality setting which can be estimated with enough confidence.
    /// </summary>
    public const int QualityEstimationConfidenceUpperThreshold = 98;

    /// <summary>
    /// Gets unscaled luminance quantization table.
    /// </summary>
    /// <remarks>
    /// The values are derived from ITU section K.1.
    /// </remarks>
    // The C# compiler emits this as a compile-time constant embedded in the PE file.
    // This is effectively compiled down to: return new ReadOnlySpan<byte>(&data, length)
    // More details can be found: https://github.com/dotnet/roslyn/pull/24621
    public static ReadOnlySpan<byte> LuminanceTable =>
    [
        16, 11, 10, 16,  24,  40,  51,  61,
        12, 12, 14, 19,  26,  58,  60,  55,
        14, 13, 16, 24,  40,  57,  69,  56,
        14, 17, 22, 29,  51,  87,  80,  62,
        18, 22, 37, 56,  68, 109, 103,  77,
        24, 35, 55, 64,  81, 104, 113,  92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103,  99
    ];

    /// <summary>
    /// Gets unscaled chrominance quantization table.
    /// </summary>
    /// <remarks>
    /// The values are derived from ITU section K.1.
    /// </remarks>
    // The C# compiler emits this as a compile-time constant embedded in the PE file.
    // This is effectively compiled down to: return new ReadOnlySpan<byte>(&data, length)
    // More details can be found: https://github.com/dotnet/roslyn/pull/24621
    public static ReadOnlySpan<byte> ChrominanceTable =>
    [
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
    ];

    /// Ported from JPEGsnoop:
    /// https://github.com/ImpulseAdventure/JPEGsnoop/blob/9732ee0961f100eb69bbff4a0c47438d5997abee/source/JfifDecode.cpp#L4570-L4694
    /// <summary>
    /// Estimates jpeg quality based on standard quantization table.
    /// </summary>
    /// <remarks>
    /// Technically, this can be used with any given table but internal decoder code uses ITU spec tables:
    /// <see cref="LuminanceTable"/> and <see cref="ChrominanceTable"/>.
    /// </remarks>
    /// <param name="table">Input quantization table.</param>
    /// <param name="target">Natural order quantization table to estimate against.</param>
    /// <returns>Estimated quality.</returns>
    public static int EstimateQuality(ref Block8x8F table, ReadOnlySpan<byte> target)
    {
        // This method can be SIMD'ified if standard table is injected as Block8x8F.
        // Or when we go to full-int16 spectral code implementation and inject both tables as Block8x8.
        double comparePercent;
        double sumPercent = 0;

        // Corner case - all 1's => 100 quality
        // It would fail to deduce using algorithm below without this check
        if (table.EqualsToScalar(1))
        {
            // While this is a 100% to be 100 quality, any given table can be scaled to all 1's.
            // According to jpeg creators, top of the line quality is 99, 100 is just a technical 'limit' which will affect result filesize drastically.
            // Quality=100 shouldn't be used in usual use case.
            return 100;
        }

        int quality;
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            int coeff = (int)table[i];

            // Coefficients are actually int16 casted to float numbers so there's no truncating error.
            if (coeff != 0)
            {
                comparePercent = 100.0 * (table[i] / target[i]);
            }
            else
            {
                // No 'valid' quantization table should contain zero at any position
                // while this is okay to decode with, it will throw DivideByZeroException at encoding proces stage.
                // Not sure what to do here, we can't throw as this technically correct
                // but this will screw up the encoder.
                comparePercent = 999.99;
            }

            sumPercent += comparePercent;
        }

        // Perform some statistical analysis of the quality factor
        // to determine the likelihood of the current quantization
        // table being a scaled version of the "standard" tables.
        // If the variance is high, it is unlikely to be the case.
        sumPercent /= 64.0;

        // Generate the equivalent IJQ "quality" factor
        if (sumPercent <= 100.0)
        {
            quality = (int)Math.Round((200 - sumPercent) / 2);
        }
        else
        {
            quality = (int)Math.Round(5000.0 / sumPercent);
        }

        return Numerics.Clamp(quality, MinQualityFactor, MaxQualityFactor);
    }

    /// <summary>
    /// Estimates jpeg quality based on quantization table in zig-zag order.
    /// </summary>
    /// <param name="luminanceTable">Luminance quantization table.</param>
    /// <returns>Estimated quality</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EstimateLuminanceQuality(ref Block8x8F luminanceTable)
        => EstimateQuality(ref luminanceTable, LuminanceTable);

    /// <summary>
    /// Estimates jpeg quality based on quantization table in zig-zag order.
    /// </summary>
    /// <param name="chrominanceTable">Chrominance quantization table.</param>
    /// <returns>Estimated quality</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EstimateChrominanceQuality(ref Block8x8F chrominanceTable)
        => EstimateQuality(ref chrominanceTable, ChrominanceTable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int QualityToScale(int quality)
    {
        DebugGuard.MustBeBetweenOrEqualTo(quality, MinQualityFactor, MaxQualityFactor, nameof(quality));

        return quality < 50 ? (5000 / quality) : (200 - (quality * 2));
    }

    public static Block8x8F ScaleQuantizationTable(int scale, ReadOnlySpan<byte> unscaledTable)
    {
        Block8x8F table = default;
        for (int j = 0; j < Block8x8F.Size; j++)
        {
            int x = ((unscaledTable[j] * scale) + 50) / 100;
            table[j] = Numerics.Clamp(x, 1, 255);
        }

        return table;
    }

    public static Block8x8 ScaleQuantizationTable(int quality, Block8x8 unscaledTable)
    {
        int scale = QualityToScale(quality);
        Block8x8 table = default;
        for (int j = 0; j < Block8x8.Size; j++)
        {
            int x = ((unscaledTable[j] * scale) + 50) / 100;
            table[j] = (short)(uint)Numerics.Clamp(x, 1, 255);
        }

        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Block8x8F ScaleLuminanceTable(int quality)
        => ScaleQuantizationTable(scale: QualityToScale(quality), LuminanceTable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Block8x8F ScaleChrominanceTable(int quality)
        => ScaleQuantizationTable(scale: QualityToScale(quality), ChrominanceTable);
}
