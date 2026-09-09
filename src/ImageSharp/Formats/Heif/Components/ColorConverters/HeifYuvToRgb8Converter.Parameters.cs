// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides fixed-point scalar and SIMD coefficient storage for eight-bit 4:2:0 conversion.
/// </content>
internal static partial class HeifYuvToRgb8Converter
{
    /// <summary>
    /// Stores every scalar and SIMD coefficient representation resolved once for an image.
    /// </summary>
    private readonly struct ConversionParameters
    {
        /// <summary>
        /// The scalar fixed-point coefficients.
        /// </summary>
        public readonly FixedPointParameters FixedPointScalar;

        /// <summary>
        /// The four-lane SIMD coefficients.
        /// </summary>
        public readonly Vector128Parameters FixedPointFourLane;

        /// <summary>
        /// The eight-lane SIMD coefficients.
        /// </summary>
        public readonly Vector256Parameters FixedPointEightLane;

        /// <summary>
        /// The sixteen-lane SIMD coefficients.
        /// </summary>
        public readonly Vector512Parameters FixedPointSixteenLane;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversionParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The shared floating-point conversion parameters.</param>
        public ConversionParameters(in HeifColorConversionParameters parameters)
        {
            FixedPointParameters scalar = new(in parameters);
            this.FixedPointScalar = scalar;
            this.FixedPointFourLane = new(in scalar);
            this.FixedPointEightLane = new(in scalar);
            this.FixedPointSixteenLane = new(in scalar);
        }
    }

    /// <summary>
    /// Stores the scalar fixed-point coefficients resolved for one image.
    /// </summary>
    private readonly struct FixedPointParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FixedPointParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The shared floating-point conversion parameters.</param>
        public FixedPointParameters(in HeifColorConversionParameters parameters)
        {
            float scale = 1 << CoefficientShift;

            // Rounding each image-invariant coefficient once gives the integer kernel eight fractional bits.
            // The signed green coefficients retain the exact addition and rounding order used by every SIMD lane.
            this.RedCr = (int)MathF.Round(parameters.RedChromaScale * scale, MidpointRounding.AwayFromZero);
            this.GreenCb = -(int)MathF.Round(parameters.GreenBlueChromaScale * scale, MidpointRounding.AwayFromZero);
            this.GreenCr = -(int)MathF.Round(parameters.GreenRedChromaScale * scale, MidpointRounding.AwayFromZero);
            this.BlueCb = (int)MathF.Round(parameters.BlueChromaScale * scale, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Gets the red contribution from centered Cr.
        /// </summary>
        public int RedCr { get; }

        /// <summary>
        /// Gets the green contribution from centered Cb.
        /// </summary>
        public int GreenCb { get; }

        /// <summary>
        /// Gets the green contribution from centered Cr.
        /// </summary>
        public int GreenCr { get; }

        /// <summary>
        /// Gets the blue contribution from centered Cb.
        /// </summary>
        public int BlueCb { get; }
    }

    /// <summary>
    /// Broadcasts the fixed-point coefficients for four-lane conversion.
    /// </summary>
    private readonly struct Vector128Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vector128Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar fixed-point coefficients.</param>
        public Vector128Parameters(in FixedPointParameters parameters)
        {
            this.ChromaMidpoint = Vector128.Create(HeifYuvToRgb8Converter.ChromaMidpoint);
            this.RoundingBias = Vector128.Create(HeifYuvToRgb8Converter.RoundingBias);
            this.Maximum = Vector128.Create((int)byte.MaxValue);
            this.RedCr = Vector128.Create(parameters.RedCr);
            this.GreenCb = Vector128.Create(parameters.GreenCb);
            this.GreenCr = Vector128.Create(parameters.GreenCr);
            this.BlueCb = Vector128.Create(parameters.BlueCb);
        }

        /// <summary>
        /// Gets the neutral chroma code-value lanes.
        /// </summary>
        public Vector128<int> ChromaMidpoint { get; }

        /// <summary>
        /// Gets the fixed-point rounding-bias lanes.
        /// </summary>
        public Vector128<int> RoundingBias { get; }

        /// <summary>
        /// Gets the maximum eight-bit sample lanes.
        /// </summary>
        public Vector128<int> Maximum { get; }

        /// <summary>
        /// Gets the red Cr coefficient lanes.
        /// </summary>
        public Vector128<int> RedCr { get; }

        /// <summary>
        /// Gets the green Cb coefficient lanes.
        /// </summary>
        public Vector128<int> GreenCb { get; }

        /// <summary>
        /// Gets the green Cr coefficient lanes.
        /// </summary>
        public Vector128<int> GreenCr { get; }

        /// <summary>
        /// Gets the blue Cb coefficient lanes.
        /// </summary>
        public Vector128<int> BlueCb { get; }
    }

    /// <summary>
    /// Broadcasts the fixed-point coefficients for eight-lane conversion.
    /// </summary>
    private readonly struct Vector256Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vector256Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar fixed-point coefficients.</param>
        public Vector256Parameters(in FixedPointParameters parameters)
        {
            this.ChromaMidpoint = Vector256.Create(HeifYuvToRgb8Converter.ChromaMidpoint);
            this.RoundingBias = Vector256.Create(HeifYuvToRgb8Converter.RoundingBias);
            this.Maximum = Vector256.Create((int)byte.MaxValue);
            this.RedCr = Vector256.Create(parameters.RedCr);
            this.GreenCb = Vector256.Create(parameters.GreenCb);
            this.GreenCr = Vector256.Create(parameters.GreenCr);
            this.BlueCb = Vector256.Create(parameters.BlueCb);
        }

        /// <summary>
        /// Gets the neutral chroma code-value lanes.
        /// </summary>
        public Vector256<int> ChromaMidpoint { get; }

        /// <summary>
        /// Gets the fixed-point rounding-bias lanes.
        /// </summary>
        public Vector256<int> RoundingBias { get; }

        /// <summary>
        /// Gets the maximum eight-bit sample lanes.
        /// </summary>
        public Vector256<int> Maximum { get; }

        /// <summary>
        /// Gets the red Cr coefficient lanes.
        /// </summary>
        public Vector256<int> RedCr { get; }

        /// <summary>
        /// Gets the green Cb coefficient lanes.
        /// </summary>
        public Vector256<int> GreenCb { get; }

        /// <summary>
        /// Gets the green Cr coefficient lanes.
        /// </summary>
        public Vector256<int> GreenCr { get; }

        /// <summary>
        /// Gets the blue Cb coefficient lanes.
        /// </summary>
        public Vector256<int> BlueCb { get; }
    }

    /// <summary>
    /// Broadcasts the fixed-point coefficients for sixteen-lane conversion.
    /// </summary>
    private readonly struct Vector512Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vector512Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar fixed-point coefficients.</param>
        public Vector512Parameters(in FixedPointParameters parameters)
        {
            this.ChromaMidpoint = Vector512.Create(HeifYuvToRgb8Converter.ChromaMidpoint);
            this.RoundingBias = Vector512.Create(HeifYuvToRgb8Converter.RoundingBias);
            this.Maximum = Vector512.Create((int)byte.MaxValue);
            this.RedCr = Vector512.Create(parameters.RedCr);
            this.GreenCb = Vector512.Create(parameters.GreenCb);
            this.GreenCr = Vector512.Create(parameters.GreenCr);
            this.BlueCb = Vector512.Create(parameters.BlueCb);
        }

        /// <summary>
        /// Gets the neutral chroma code-value lanes.
        /// </summary>
        public Vector512<int> ChromaMidpoint { get; }

        /// <summary>
        /// Gets the fixed-point rounding-bias lanes.
        /// </summary>
        public Vector512<int> RoundingBias { get; }

        /// <summary>
        /// Gets the maximum eight-bit sample lanes.
        /// </summary>
        public Vector512<int> Maximum { get; }

        /// <summary>
        /// Gets the red Cr coefficient lanes.
        /// </summary>
        public Vector512<int> RedCr { get; }

        /// <summary>
        /// Gets the green Cb coefficient lanes.
        /// </summary>
        public Vector512<int> GreenCb { get; }

        /// <summary>
        /// Gets the green Cr coefficient lanes.
        /// </summary>
        public Vector512<int> GreenCr { get; }

        /// <summary>
        /// Gets the blue Cb coefficient lanes.
        /// </summary>
        public Vector512<int> BlueCb { get; }
    }
}
