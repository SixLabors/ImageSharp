// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Provides methods and properties related to jpeg quantization.
    /// </summary>
    internal static class Quantization
    {
        /// <summary>
        /// Threshold at which given luminance quantization table should not be considered 'standard'.
        /// </summary>
        /// <remarks>
        /// Jpeg does not define either 'quality' nor 'standard quantization table' properties
        /// so this is purely a practical value derived from tests.
        /// </remarks>
        public const double StandardLuminanceTableVarianceThreshold = 10.0;

        /// <summary>
        /// Threshold at which given luminance quantization table should not be considered 'standard'.
        /// </summary>
        /// <remarks>
        /// Jpeg does not define either 'quality' nor 'standard quantization table' properties
        /// so this is purely a practical value derived from tests.
        /// </remarks>
        public const double StandardChrominanceTableVarianceThreshold = 10.0;

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

        // https://github.com/ImpulseAdventure/JPEGsnoop/blob/9732ee0961f100eb69bbff4a0c47438d5997abee/source/JfifDecode.cpp#L4570-L4694
        public static void EstimateQuality(ref Block8x8F table, ReadOnlySpan<byte> target, out double quality, out double variance)
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
                // According to jpeg creators, top of the line quality is 99, 100 is just a technical 'limit'.
                quality = 100;
                variance = 0;
                return;
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
            variance = sumPercentSqr - (sumPercent * sumPercent);

            // Generate the equivalent IJQ "quality" factor
            if (sumPercent <= 100.0)
            {
                quality = (200 - sumPercent) / 2;
            }
            else
            {
                quality = 5000.0 / sumPercent;
            }
        }
    }
}
