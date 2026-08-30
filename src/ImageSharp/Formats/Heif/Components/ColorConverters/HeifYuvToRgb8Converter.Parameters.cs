// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

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
        /// The scalar pinned-libheif conversion parameters.
        /// </summary>
        public readonly LibheifParameters LibheifScalar;

        /// <summary>
        /// The four-lane pinned-libheif conversion parameters.
        /// </summary>
        public readonly LibheifVector128Parameters LibheifFourLane;

        /// <summary>
        /// The eight-lane pinned-libheif conversion parameters.
        /// </summary>
        public readonly LibheifVector256Parameters LibheifEightLane;

        /// <summary>
        /// The sixteen-lane pinned-libheif conversion parameters.
        /// </summary>
        public readonly LibheifVector512Parameters LibheifSixteenLane;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversionParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The shared floating-point conversion parameters.</param>
        /// <param name="bitDepth">The common source component precision.</param>
        public ConversionParameters(in HeifColorConversionParameters parameters, int bitDepth)
        {
            FixedPointParameters scalar = new(in parameters);
            this.FixedPointScalar = scalar;
            this.FixedPointFourLane = new(in scalar);
            this.FixedPointEightLane = new(in scalar);
            this.FixedPointSixteenLane = new(in scalar);

            LibheifParameters libheif = new(in parameters, bitDepth);
            this.LibheifScalar = libheif;
            this.LibheifFourLane = new(in libheif);
            this.LibheifEightLane = new(in libheif);
            this.LibheifSixteenLane = new(in libheif);
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

    /// <summary>
    /// Stores the scalar coefficients and range values used by pinned libheif 1.23.1.
    /// </summary>
    private readonly struct LibheifParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LibheifParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The resolved H.273 matrix and range values.</param>
        /// <param name="bitDepth">The common source component precision.</param>
        public LibheifParameters(in HeifColorConversionParameters parameters, int bitDepth)
        {
            this.LumaOffset = parameters.IsFullRange ? 0F : parameters.LumaBias;
            this.LumaScale = parameters.IsFullRange ? 1F : 1.1689F;
            this.ChromaMidpoint = parameters.ChromaBias;
            this.ChromaScale = parameters.IsFullRange ? 1F : 1.1429F;
            if (parameters.MatrixCoefficients == CicpMatrixCoefficients.Unspecified)
            {
                // libheif falls back to these literal Rec.601 coefficients when no matrix is signaled.
                this.RedCr = 1.402F;
                this.GreenCb = -0.344136F;
                this.GreenCr = -0.714136F;
                this.BlueCb = 1.772F;
            }
            else
            {
                float kr = parameters.Kr;
                float kb = parameters.Kb;
                this.RedCr = 2F * (-kr + 1F);
                this.GreenCb = 2F * kb * (-kb + 1F) / (kb + kr - 1F);
                this.GreenCr = 2F * kr * (-kr + 1F) / (kb + kr - 1F);
                this.BlueCb = 2F * (-kb + 1F);
            }

            this.Maximum = (1 << bitDepth) - 1;
            this.OutputShift = bitDepth - 8;
        }

        /// <summary>
        /// Gets the luma code-value offset removed before limited-range expansion.
        /// </summary>
        public float LumaOffset { get; }

        /// <summary>
        /// Gets the luma range-expansion factor.
        /// </summary>
        public float LumaScale { get; }

        /// <summary>
        /// Gets the neutral chroma code value.
        /// </summary>
        public float ChromaMidpoint { get; }

        /// <summary>
        /// Gets the chroma range-expansion factor.
        /// </summary>
        public float ChromaScale { get; }

        /// <summary>
        /// Gets the red contribution from Cr.
        /// </summary>
        public float RedCr { get; }

        /// <summary>
        /// Gets the green contribution from Cb.
        /// </summary>
        public float GreenCb { get; }

        /// <summary>
        /// Gets the green contribution from Cr.
        /// </summary>
        public float GreenCr { get; }

        /// <summary>
        /// Gets the blue contribution from Cb.
        /// </summary>
        public float BlueCb { get; }

        /// <summary>
        /// Gets the largest RGB code value at the source precision.
        /// </summary>
        public int Maximum { get; }

        /// <summary>
        /// Gets the right shift reducing source-precision RGB to eight bits.
        /// </summary>
        public int OutputShift { get; }
    }

    /// <summary>
    /// Broadcasts pinned-libheif coefficients for four-lane conversion.
    /// </summary>
    private readonly struct LibheifVector128Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LibheifVector128Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar pinned-libheif coefficients.</param>
        public LibheifVector128Parameters(in LibheifParameters parameters)
        {
            this.LumaOffset = Vector128.Create(parameters.LumaOffset);
            this.LumaScale = Vector128.Create(parameters.LumaScale);
            this.ChromaMidpoint = Vector128.Create(parameters.ChromaMidpoint);
            this.ChromaScale = Vector128.Create(parameters.ChromaScale);
            this.RedCr = Vector128.Create(parameters.RedCr);
            this.GreenCb = Vector128.Create(parameters.GreenCb);
            this.GreenCr = Vector128.Create(parameters.GreenCr);
            this.BlueCb = Vector128.Create(parameters.BlueCb);
            this.Maximum = Vector128.Create(parameters.Maximum);
            this.OutputShift = parameters.OutputShift;
        }

        /// <summary>Gets the luma offset lanes.</summary>
        public Vector128<float> LumaOffset { get; }

        /// <summary>Gets the luma scale lanes.</summary>
        public Vector128<float> LumaScale { get; }

        /// <summary>Gets the chroma-midpoint lanes.</summary>
        public Vector128<float> ChromaMidpoint { get; }

        /// <summary>Gets the chroma-scale lanes.</summary>
        public Vector128<float> ChromaScale { get; }

        /// <summary>Gets the red Cr coefficient lanes.</summary>
        public Vector128<float> RedCr { get; }

        /// <summary>Gets the green Cb coefficient lanes.</summary>
        public Vector128<float> GreenCb { get; }

        /// <summary>Gets the green Cr coefficient lanes.</summary>
        public Vector128<float> GreenCr { get; }

        /// <summary>Gets the blue Cb coefficient lanes.</summary>
        public Vector128<float> BlueCb { get; }

        /// <summary>Gets the maximum RGB code-value lanes.</summary>
        public Vector128<int> Maximum { get; }

        /// <summary>Gets the output reduction shift.</summary>
        public int OutputShift { get; }
    }

    /// <summary>
    /// Broadcasts pinned-libheif coefficients for eight-lane conversion.
    /// </summary>
    private readonly struct LibheifVector256Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LibheifVector256Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar pinned-libheif coefficients.</param>
        public LibheifVector256Parameters(in LibheifParameters parameters)
        {
            this.LumaOffset = Vector256.Create(parameters.LumaOffset);
            this.LumaScale = Vector256.Create(parameters.LumaScale);
            this.ChromaMidpoint = Vector256.Create(parameters.ChromaMidpoint);
            this.ChromaScale = Vector256.Create(parameters.ChromaScale);
            this.RedCr = Vector256.Create(parameters.RedCr);
            this.GreenCb = Vector256.Create(parameters.GreenCb);
            this.GreenCr = Vector256.Create(parameters.GreenCr);
            this.BlueCb = Vector256.Create(parameters.BlueCb);
            this.Maximum = Vector256.Create(parameters.Maximum);
            this.OutputShift = parameters.OutputShift;
        }

        /// <summary>Gets the luma offset lanes.</summary>
        public Vector256<float> LumaOffset { get; }

        /// <summary>Gets the luma scale lanes.</summary>
        public Vector256<float> LumaScale { get; }

        /// <summary>Gets the chroma-midpoint lanes.</summary>
        public Vector256<float> ChromaMidpoint { get; }

        /// <summary>Gets the chroma-scale lanes.</summary>
        public Vector256<float> ChromaScale { get; }

        /// <summary>Gets the red Cr coefficient lanes.</summary>
        public Vector256<float> RedCr { get; }

        /// <summary>Gets the green Cb coefficient lanes.</summary>
        public Vector256<float> GreenCb { get; }

        /// <summary>Gets the green Cr coefficient lanes.</summary>
        public Vector256<float> GreenCr { get; }

        /// <summary>Gets the blue Cb coefficient lanes.</summary>
        public Vector256<float> BlueCb { get; }

        /// <summary>Gets the maximum RGB code-value lanes.</summary>
        public Vector256<int> Maximum { get; }

        /// <summary>Gets the output reduction shift.</summary>
        public int OutputShift { get; }
    }

    /// <summary>
    /// Broadcasts pinned-libheif coefficients for sixteen-lane conversion.
    /// </summary>
    private readonly struct LibheifVector512Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LibheifVector512Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar pinned-libheif coefficients.</param>
        public LibheifVector512Parameters(in LibheifParameters parameters)
        {
            this.LumaOffset = Vector512.Create(parameters.LumaOffset);
            this.LumaScale = Vector512.Create(parameters.LumaScale);
            this.ChromaMidpoint = Vector512.Create(parameters.ChromaMidpoint);
            this.ChromaScale = Vector512.Create(parameters.ChromaScale);
            this.RedCr = Vector512.Create(parameters.RedCr);
            this.GreenCb = Vector512.Create(parameters.GreenCb);
            this.GreenCr = Vector512.Create(parameters.GreenCr);
            this.BlueCb = Vector512.Create(parameters.BlueCb);
            this.Maximum = Vector512.Create(parameters.Maximum);
            this.OutputShift = parameters.OutputShift;
        }

        /// <summary>Gets the luma offset lanes.</summary>
        public Vector512<float> LumaOffset { get; }

        /// <summary>Gets the luma scale lanes.</summary>
        public Vector512<float> LumaScale { get; }

        /// <summary>Gets the chroma-midpoint lanes.</summary>
        public Vector512<float> ChromaMidpoint { get; }

        /// <summary>Gets the chroma-scale lanes.</summary>
        public Vector512<float> ChromaScale { get; }

        /// <summary>Gets the red Cr coefficient lanes.</summary>
        public Vector512<float> RedCr { get; }

        /// <summary>Gets the green Cb coefficient lanes.</summary>
        public Vector512<float> GreenCb { get; }

        /// <summary>Gets the green Cr coefficient lanes.</summary>
        public Vector512<float> GreenCr { get; }

        /// <summary>Gets the blue Cb coefficient lanes.</summary>
        public Vector512<float> BlueCb { get; }

        /// <summary>Gets the maximum RGB code-value lanes.</summary>
        public Vector512<int> Maximum { get; }

        /// <summary>Gets the output reduction shift.</summary>
        public int OutputShift { get; }
    }
}
