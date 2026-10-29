// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Evaluates legacy lut8Type and lut16Type tables through the shared scalar and SIMD paths.
    /// </summary>
    /// <remarks>
    /// These tables store a matrix, input curves, a CLUT and output curves, in that order.
    /// ICC.1 sections 10.10 and 10.11 permit the matrix only for PCSXYZ input.
    /// Lab also has three components, but its values must enter the input curves without this matrix.
    /// The caller supplies the device space for AToB tables and the PCS for BToA tables.
    /// This restriction belongs to the legacy table types; mAB and mBA tables use <see cref="LutColorOperator"/> and their own matrix order.
    /// </remarks>
    internal readonly struct SampledLutColorOperator : IIccColorConverterOperator
    {
        private readonly LutTrcOperator input;
        private readonly LutTrcOperator output;
        private readonly MatrixOperator matrix;
        private readonly ClutOperator clut;
        private readonly bool hasMatrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="SampledLutColorOperator"/> struct for an eight-bit LUT.
        /// </summary>
        /// <param name="entry">The parsed LUT and its sampled channel tables.</param>
        /// <param name="inputColorSpace">The device space for an AToB table, or the PCS for a BToA table. Only XYZ enables the matrix.</param>
        /// <param name="trilinear">Whether to use multilinear interpolation.</param>
        public SampledLutColorOperator(IccLut8TagDataEntry entry, IccColorSpaceType inputColorSpace, bool trilinear)
            : this(entry.InputValues, entry.OutputValues, entry.ClutValues, entry.Matrix, inputColorSpace, trilinear, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SampledLutColorOperator"/> struct for a sixteen-bit LUT.
        /// </summary>
        /// <param name="entry">The parsed LUT and its sampled channel tables.</param>
        /// <param name="inputColorSpace">The device space for an AToB table, or the PCS for a BToA table. Only XYZ enables the matrix.</param>
        /// <param name="trilinear">Whether to use multilinear interpolation.</param>
        public SampledLutColorOperator(IccLut16TagDataEntry entry, IccColorSpaceType inputColorSpace, bool trilinear)
            : this(entry.InputValues, entry.OutputValues, entry.ClutValues, entry.Matrix, inputColorSpace, trilinear, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SampledLutColorOperator"/> struct.
        /// </summary>
        /// <param name="input">The selected input channel tables.</param>
        /// <param name="output">The selected output channel tables.</param>
        /// <param name="clut">The parsed color lookup table.</param>
        /// <param name="matrix">The stored XYZ matrix, ignored for non-XYZ input.</param>
        /// <param name="inputColorSpace">The color space entering this LUT, before its input tables.</param>
        /// <param name="trilinear">Whether to use multilinear interpolation.</param>
        /// <param name="is16Bit">Whether the tag uses legacy sixteen-bit PCS encoding.</param>
        private SampledLutColorOperator(
            IccLut[] input,
            IccLut[] output,
            IccClut clut,
            Matrix4x4 matrix,
            IccColorSpaceType inputColorSpace,
            bool trilinear,
            bool is16Bit)
        {
            // Both legacy LUT types use sampled tables. Their direction is defined by their position around the CLUT.
            this.input = new LutTrcOperator(input);
            this.output = new LutTrcOperator(output);
            this.matrix = new MatrixOperator(matrix, Vector3.Zero);
            this.clut = new ClutOperator(clut, trilinear);

            // ICC.1 sections 10.10 and 10.11 permit this legacy matrix only for XYZ input; Lab's three channels do not qualify.
            // Select the matrix once during setup. Scalar and SIMD conversion then use the same decision.
            // The Generic CMYK profiles in Issue2454_A/B contain a 1/16 matrix in their Lab-to-CMYK tables.
            // Applying that matrix to Lab changes lightness and chroma before the lookup, producing incorrect ink amounts.
            // Ignore this unused matrix for non-XYZ input; retain its original values in the parsed profile for serialization.
            this.hasMatrix = inputColorSpace == IccColorSpaceType.CieXyz && clut.InputChannelCount == 3 && !matrix.IsIdentity;
            this.Is16BitLut = is16Bit;
        }

        /// <inheritdoc/>
        public bool IsTrc => false;

        /// <inheritdoc/>
        public bool Is16BitLut { get; }

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            value = this.hasMatrix ? this.matrix.Convert(value) : value;
            return this.output.Convert(this.clut.Convert(this.input.Convert(value)));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // The stage types are fixed at setup. Keep each component in its register between stages.
            if (this.hasMatrix)
            {
                this.matrix.Convert(ref x, ref y, ref z);
            }

            this.input.Convert(ref x, ref y, ref z, ref w);
            this.clut.Convert(ref x, ref y, ref z, ref w);
            this.output.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // The stage types are fixed at setup. Keep each component in its register between stages.
            if (this.hasMatrix)
            {
                this.matrix.Convert(ref x, ref y, ref z);
            }

            this.input.Convert(ref x, ref y, ref z, ref w);
            this.clut.Convert(ref x, ref y, ref z, ref w);
            this.output.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // The stage types are fixed at setup. Keep each component in its register between stages.
            if (this.hasMatrix)
            {
                this.matrix.Convert(ref x, ref y, ref z);
            }

            this.input.Convert(ref x, ref y, ref z, ref w);
            this.clut.Convert(ref x, ref y, ref z, ref w);
            this.output.Convert(ref x, ref y, ref z, ref w);
        }
    }
}
