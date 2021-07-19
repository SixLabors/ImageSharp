// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
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
        /// Threshold at which given luminance quantization table should be considered 'standard'.
        /// Bigger the variance - more likely it to be a non-ITU complient table.
        /// </summary>
        /// <remarks>
        /// Jpeg does not define either 'quality' nor 'standard quantization table' properties
        /// so this is purely a practical value derived from tests.
        /// For actual variances output against standard table see tests at Formats.Jpg.QuantizationTests.PrintVariancesFromStandardTables_*.
        /// Actual value is 2.3629059983706604, truncated unsignificant part.
        /// </remarks>
        public const double StandardLuminanceTableVarianceThreshold = 2.36291;

        /// <summary>
        /// Threshold at which given chrominance quantization table should be considered 'standard'.
        /// Bigger the variance - more likely it to be a non-ITU complient table.
        /// </summary>
        /// <remarks>
        /// Jpeg does not define either 'quality' nor 'standard quantization table' properties
        /// so this is purely a practical value derived from tests.
        /// For actual variances output against standard table see tests at Formats.Jpg.QuantizationTests.PrintVariancesFromStandardTables_*.
        /// Actual value is 0.8949631033036098, truncated unsignificant part.
        /// </remarks>
        public const double StandardChrominanceTableVarianceThreshold = 0.894963;

        /// <summary>
        /// Gets the unscaled luminance quantization table in zig-zag order. Each
        /// encoder copies and scales the tables according to its quality parameter.
        /// The values are derived from ITU section K.1 after converting from natural to
        /// zig-zag order.
        /// </summary>
        // The C# compiler emits this as a compile-time constant embedded in the PE file.
        // This is effectively compiled down to: return new ReadOnlySpan<byte>(&data, length)
        // More details can be found: https://github.com/dotnet/roslyn/pull/24621
        public static ReadOnlySpan<byte> UnscaledQuant_Luminance => new byte[]
        {
            16, 11, 12, 14, 12, 10, 16, 14, 13, 14, 18, 17, 16, 19, 24,
            40, 26, 24, 22, 22, 24, 49, 35, 37, 29, 40, 58, 51, 61, 60,
            57, 51, 56, 55, 64, 72, 92, 78, 64, 68, 87, 69, 55, 56, 80,
            109, 81, 87, 95, 98, 103, 104, 103, 62, 77, 113, 121, 112,
            100, 120, 92, 101, 103, 99,
        };

        /// <summary>
        /// Gets the unscaled chrominance quantization table in zig-zag order. Each
        /// encoder copies and scales the tables according to its quality parameter.
        /// The values are derived from ITU section K.1 after converting from natural to
        /// zig-zag order.
        /// </summary>
        // The C# compiler emits this as a compile-time constant embedded in the PE file.
        // This is effectively compiled down to: return new ReadOnlySpan<byte>(&data, length)
        // More details can be found: https://github.com/dotnet/roslyn/pull/24621
        public static ReadOnlySpan<byte> UnscaledQuant_Chrominance => new byte[]
        {
            17, 18, 18, 24, 21, 24, 47, 26, 26, 47, 99, 66, 56, 66,
            99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
        };

        /// Ported from JPEGsnoop:
        /// https://github.com/ImpulseAdventure/JPEGsnoop/blob/9732ee0961f100eb69bbff4a0c47438d5997abee/source/JfifDecode.cpp#L4570-L4694
        /// <summary>
        /// Estimates jpeg quality based on quantization table in zig-zag order.
        /// </summary>
        /// <remarks>
        /// This technically can be used with any given table but internal decoder code uses ITU spec tables:
        /// <see cref="UnscaledQuant_Luminance"/> and <see cref="UnscaledQuant_Chrominance"/>.
        /// </remarks>
        /// <param name="table">Input quantization table.</param>
        /// <param name="target">Quantization to estimate against.</param>
        /// <param name="quality">Estimated quality</param>
        /// <returns><see cref="bool"/> indicating if given table is target-complient</returns>
        public static double EstimateQuality(ref Block8x8F table, ReadOnlySpan<byte> target, out int quality)
        {
            // This method can be SIMD'ified if standard table is injected as Block8x8F.
            // Or when we go to full-int16 spectral code implementation and inject both tables as Block8x8.
            double comparePercent;
            double sumPercent = 0;
            double sumPercentSqr = 0;

            // Corner case - all 1's => 100 quality
            // It would fail to deduce using algorithm below without this check
            if (table.EqualsToScalar(1))
            {
                // While this is a 100% to be 100 quality, any given table can be scaled to all 1's.
                // According to jpeg creators, top of the line quality is 99, 100 is just a technical 'limit' will affect result filesize drastically.
                // Quality=100 shouldn't be used in usual use case.
                quality = 100;
                return 0;
            }

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                float coeff = table[i];
                int coeffInteger = (int)coeff;

                // Coefficients are actually int16 casted to float numbers so there's no truncating error.
                if (coeffInteger != 0)
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
                sumPercentSqr += comparePercent * comparePercent;
            }

            // Perform some statistical analysis of the quality factor
            // to determine the likelihood of the current quantization
            // table being a scaled version of the "standard" tables.
            // If the variance is high, it is unlikely to be the case.
            sumPercent /= 64.0;
            sumPercentSqr /= 64.0;

            // Generate the equivalent IJQ "quality" factor
            if (sumPercent <= 100.0)
            {
                quality = (int)Math.Round((200 - sumPercent) / 2);
            }
            else
            {
                quality = (int)Math.Round(5000.0 / sumPercent);
            }

            return sumPercentSqr - (sumPercent * sumPercent);
        }

        /// <summary>
        /// Estimates jpeg quality based on quantization table in zig-zag order.
        /// </summary>
        /// <param name="luminanceTable">Luminance quantization table.</param>
        /// <param name="quality">Output jpeg quality.</param>
        /// <returns><see cref="bool"/> indicating if given table is ITU-complient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EstimateLuminanceQuality(ref Block8x8F luminanceTable, out int quality)
        {
            double variance = EstimateQuality(ref luminanceTable, UnscaledQuant_Luminance, out quality);
            return variance <= StandardLuminanceTableVarianceThreshold;
        }

        /// <summary>
        /// Estimates jpeg quality based on quantization table in zig-zag order.
        /// </summary>
        /// <param name="chrominanceTable">Chrominance quantization table.</param>
        /// <param name="quality">Output jpeg quality.</param>
        /// <returns><see cref="bool"/> indicating if given table is ITU-complient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EstimateChrominanceQuality(ref Block8x8F chrominanceTable, out int quality)
        {
            double variance = EstimateQuality(ref chrominanceTable, UnscaledQuant_Chrominance, out quality);
            return variance <= StandardChrominanceTableVarianceThreshold;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int QualityToScale(int quality)
            => quality < 50 ? 5000 / quality : 200 - (quality * 2);

        private static Block8x8F ScaleQuantizationTable(int scale, ReadOnlySpan<byte> unscaledTable)
        {
            DebugGuard.MustBeBetweenOrEqualTo(scale, MinQualityFactor, MaxQualityFactor, nameof(scale));

            Block8x8F table = default;
            for (int j = 0; j < Block8x8F.Size; j++)
            {
                int x = unscaledTable[j];
                x = ((x * scale) + 50) / 100;
                if (x < 1)
                {
                    x = 1;
                }

                if (x > 255)
                {
                    x = 255;
                }

                table[j] = x;
            }

            return table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Block8x8F ScaleLuminanceTable(int quality)
            => ScaleQuantizationTable(scale: QualityToScale(quality), UnscaledQuant_Luminance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Block8x8F ScaleChrominanceTable(int quality)
            => ScaleQuantizationTable(scale: QualityToScale(quality), UnscaledQuant_Chrominance);
    }
}
