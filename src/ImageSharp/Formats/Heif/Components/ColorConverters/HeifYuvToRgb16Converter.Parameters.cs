// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides scalar and SIMD parameter storage for high-bit-depth pinned-libheif conversion.
/// </content>
internal static partial class HeifYuvToRgb16Converter
{
    /// <summary>
    /// Stores every scalar and SIMD coefficient representation resolved once for an image.
    /// </summary>
    private readonly struct ConversionParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConversionParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The resolved H.273 matrix and range values.</param>
        /// <param name="bitDepth">The common source component precision.</param>
        public ConversionParameters(in HeifColorConversionParameters parameters, int bitDepth)
        {
            ScalarParameters scalar = new(in parameters, bitDepth);
            this.Scalar = scalar;
            this.SixteenLane = new(in scalar);
            this.EightLane = new(in scalar);
            this.FourLane = new(in scalar);
        }

        /// <summary>
        /// Gets the scalar conversion parameters.
        /// </summary>
        public ScalarParameters Scalar { get; }

        /// <summary>
        /// Gets the sixteen-lane conversion parameters.
        /// </summary>
        public Vector512Parameters SixteenLane { get; }

        /// <summary>
        /// Gets the eight-lane conversion parameters.
        /// </summary>
        public Vector256Parameters EightLane { get; }

        /// <summary>
        /// Gets the four-lane conversion parameters.
        /// </summary>
        public Vector128Parameters FourLane { get; }
    }

    /// <summary>
    /// Stores the scalar arithmetic and output scaling used by pinned libheif.
    /// </summary>
    private readonly struct ScalarParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScalarParameters"/> struct.
        /// </summary>
        /// <param name="parameters">The resolved H.273 matrix and range values.</param>
        /// <param name="bitDepth">The common source component precision.</param>
        public ScalarParameters(in HeifColorConversionParameters parameters, int bitDepth)
        {
            this.LumaOffset = parameters.IsFullRange ? 0F : parameters.LumaBias;
            this.LumaScale = parameters.IsFullRange ? 1F : 1.1689F;
            this.ChromaMidpoint = parameters.ChromaBias;
            this.ChromaScale = parameters.IsFullRange ? 1F : 1.1429F;
            if (parameters.MatrixCoefficients == CicpMatrixCoefficients.Unspecified)
            {
                // libheif falls back to these literal Rec.601 coefficients when no matrix is signaled. Deriving them
                // from Kr and Kb produces different float32 values and can move high-bit-depth green by one code value.
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
            this.OutputLeftShift = 16 - bitDepth;
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
        /// Gets the largest source-precision RGB code value.
        /// </summary>
        public int Maximum { get; }

        /// <summary>
        /// Gets the left shift mapping source-precision RGB into 16-bit pixel storage.
        /// </summary>
        public int OutputLeftShift { get; }
    }

    /// <summary>
    /// Broadcasts pinned-libheif coefficients for sixteen-lane conversion.
    /// </summary>
    private readonly struct Vector512Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vector512Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar pinned-libheif coefficients.</param>
        public Vector512Parameters(in ScalarParameters parameters)
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
        }

        /// <summary>
        /// Gets the luma offset lanes.
        /// </summary>
        public Vector512<float> LumaOffset { get; }

        /// <summary>
        /// Gets the luma scale lanes.
        /// </summary>
        public Vector512<float> LumaScale { get; }

        /// <summary>
        /// Gets the chroma-midpoint lanes.
        /// </summary>
        public Vector512<float> ChromaMidpoint { get; }

        /// <summary>
        /// Gets the chroma-scale lanes.
        /// </summary>
        public Vector512<float> ChromaScale { get; }

        /// <summary>
        /// Gets the red Cr coefficient lanes.
        /// </summary>
        public Vector512<float> RedCr { get; }

        /// <summary>
        /// Gets the green Cb coefficient lanes.
        /// </summary>
        public Vector512<float> GreenCb { get; }

        /// <summary>
        /// Gets the green Cr coefficient lanes.
        /// </summary>
        public Vector512<float> GreenCr { get; }

        /// <summary>
        /// Gets the blue Cb coefficient lanes.
        /// </summary>
        public Vector512<float> BlueCb { get; }

        /// <summary>
        /// Gets the maximum source-precision RGB lanes.
        /// </summary>
        public Vector512<int> Maximum { get; }
    }

    /// <summary>
    /// Broadcasts pinned-libheif coefficients for eight-lane conversion.
    /// </summary>
    private readonly struct Vector256Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vector256Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar pinned-libheif coefficients.</param>
        public Vector256Parameters(in ScalarParameters parameters)
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
        }

        /// <summary>
        /// Gets the luma offset lanes.
        /// </summary>
        public Vector256<float> LumaOffset { get; }

        /// <summary>
        /// Gets the luma scale lanes.
        /// </summary>
        public Vector256<float> LumaScale { get; }

        /// <summary>
        /// Gets the chroma-midpoint lanes.
        /// </summary>
        public Vector256<float> ChromaMidpoint { get; }

        /// <summary>
        /// Gets the chroma-scale lanes.
        /// </summary>
        public Vector256<float> ChromaScale { get; }

        /// <summary>
        /// Gets the red Cr coefficient lanes.
        /// </summary>
        public Vector256<float> RedCr { get; }

        /// <summary>
        /// Gets the green Cb coefficient lanes.
        /// </summary>
        public Vector256<float> GreenCb { get; }

        /// <summary>
        /// Gets the green Cr coefficient lanes.
        /// </summary>
        public Vector256<float> GreenCr { get; }

        /// <summary>
        /// Gets the blue Cb coefficient lanes.
        /// </summary>
        public Vector256<float> BlueCb { get; }

        /// <summary>
        /// Gets the maximum source-precision RGB lanes.
        /// </summary>
        public Vector256<int> Maximum { get; }
    }

    /// <summary>
    /// Broadcasts pinned-libheif coefficients for four-lane conversion.
    /// </summary>
    private readonly struct Vector128Parameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vector128Parameters"/> struct.
        /// </summary>
        /// <param name="parameters">The scalar pinned-libheif coefficients.</param>
        public Vector128Parameters(in ScalarParameters parameters)
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
        }

        /// <summary>
        /// Gets the luma offset lanes.
        /// </summary>
        public Vector128<float> LumaOffset { get; }

        /// <summary>
        /// Gets the luma scale lanes.
        /// </summary>
        public Vector128<float> LumaScale { get; }

        /// <summary>
        /// Gets the chroma-midpoint lanes.
        /// </summary>
        public Vector128<float> ChromaMidpoint { get; }

        /// <summary>
        /// Gets the chroma-scale lanes.
        /// </summary>
        public Vector128<float> ChromaScale { get; }

        /// <summary>
        /// Gets the red Cr coefficient lanes.
        /// </summary>
        public Vector128<float> RedCr { get; }

        /// <summary>
        /// Gets the green Cb coefficient lanes.
        /// </summary>
        public Vector128<float> GreenCb { get; }

        /// <summary>
        /// Gets the green Cr coefficient lanes.
        /// </summary>
        public Vector128<float> GreenCr { get; }

        /// <summary>
        /// Gets the blue Cb coefficient lanes.
        /// </summary>
        public Vector128<float> BlueCb { get; }

        /// <summary>
        /// Gets the maximum source-precision RGB lanes.
        /// </summary>
        public Vector128<int> Maximum { get; }
    }
}
