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
    /// Applies legacy LUT and mAB/mBA stages in their encoded order without per-color object dispatch.
    /// </summary>
    internal readonly struct LutColorOperator : IIccColorConverterOperator
    {
        private readonly TrcOperator curvesA;
        private readonly TrcOperator curvesB;
        private readonly TrcOperator curvesM;
        private readonly MatrixOperator matrix;
        private readonly ClutOperator clut;
        private readonly bool hasMatrix;
        private readonly bool hasClut;
        private readonly bool legacy;
        private readonly bool bToA;

        /// <summary>
        /// Initializes a new instance of the <see cref="LutColorOperator"/> struct for an eight-bit LUT.
        /// </summary>
        /// <param name="entry">The parsed LUT entry.</param>
        /// <param name="trilinear">Whether the CLUT uses multilinear interpolation.</param>
        public LutColorOperator(IccLut8TagDataEntry entry, bool trilinear)
            : this(entry.InputValues, entry.OutputValues, entry.ClutValues, entry.Matrix, trilinear, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LutColorOperator"/> struct for a sixteen-bit LUT.
        /// </summary>
        /// <param name="entry">The parsed LUT entry.</param>
        /// <param name="trilinear">Whether the CLUT uses multilinear interpolation.</param>
        public LutColorOperator(IccLut16TagDataEntry entry, bool trilinear)
            : this(entry.InputValues, entry.OutputValues, entry.ClutValues, entry.Matrix, trilinear, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LutColorOperator"/> struct for an mAB LUT.
        /// </summary>
        /// <param name="entry">The parsed A-to-B entry.</param>
        /// <param name="trilinear">Whether the CLUT uses multilinear interpolation.</param>
        public LutColorOperator(IccLutAToBTagDataEntry entry, bool trilinear)
            : this(entry.CurveA, entry.CurveB, entry.CurveM, entry.Matrix3x3, entry.Matrix3x1, entry.ClutValues, trilinear, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LutColorOperator"/> struct for an mBA LUT.
        /// </summary>
        /// <param name="entry">The parsed B-to-A entry.</param>
        /// <param name="trilinear">Whether the CLUT uses multilinear interpolation.</param>
        public LutColorOperator(IccLutBToATagDataEntry entry, bool trilinear)
            : this(entry.CurveA, entry.CurveB, entry.CurveM, entry.Matrix3x3, entry.Matrix3x1, entry.ClutValues, trilinear, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LutColorOperator"/> struct from legacy LUT stages.
        /// </summary>
        /// <param name="input">The input channel tables.</param>
        /// <param name="output">The output channel tables.</param>
        /// <param name="clut">The color table.</param>
        /// <param name="matrix">The optional three-channel matrix.</param>
        /// <param name="trilinear">Whether the CLUT uses multilinear interpolation.</param>
        /// <param name="is16Bit">Whether PCS values use legacy sixteen-bit LUT encoding.</param>
        private LutColorOperator(IccLut[] input, IccLut[] output, IccClut clut, Matrix4x4 matrix, bool trilinear, bool is16Bit)
        {
            this = default;
            this.legacy = true;
            this.curvesA = new TrcOperator(input);
            this.curvesB = new TrcOperator(output);
            this.clut = new ClutOperator(clut, trilinear);
            this.hasClut = true;
            this.matrix = new MatrixOperator(matrix, Vector3.Zero);
            this.hasMatrix = !matrix.IsIdentity && input.Length == 3;
            this.Is16BitLut = is16Bit;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LutColorOperator"/> struct from mAB or mBA stages.
        /// </summary>
        /// <param name="a">The optional A curves.</param>
        /// <param name="b">The optional B curves.</param>
        /// <param name="m">The optional M curves.</param>
        /// <param name="matrix">The optional matrix.</param>
        /// <param name="offset">The optional matrix offset.</param>
        /// <param name="clut">The optional color table.</param>
        /// <param name="trilinear">Whether the CLUT uses multilinear interpolation.</param>
        /// <param name="bToA">Whether the stages run in B-to-A order.</param>
        private LutColorOperator(
            IccTagDataEntry[]? a,
            IccTagDataEntry[]? b,
            IccTagDataEntry[]? m,
            Matrix4x4? matrix,
            Vector3? offset,
            IccClut? clut,
            bool trilinear,
            bool bToA)
        {
            this = default;
            this.bToA = bToA;

            // Tag constructors validate stage continuity. Absent curve stages are identity operators.
            this.curvesA = new TrcOperator(a, false);
            this.curvesB = new TrcOperator(b, false);
            this.curvesM = new TrcOperator(m, false);
            this.hasMatrix = matrix.HasValue && offset.HasValue;
            this.matrix = this.hasMatrix ? new MatrixOperator(matrix.GetValueOrDefault(), offset.GetValueOrDefault()) : default;
            this.hasClut = clut is not null;
            this.clut = clut is not null ? new ClutOperator(clut, trilinear) : default;
        }

        /// <inheritdoc/>
        public bool IsTrc => false;

        /// <inheritdoc/>
        public bool Is16BitLut { get; }

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            if (this.legacy)
            {
                value = this.hasMatrix ? this.matrix.Convert(value) : value;
                value = this.curvesA.Convert(value);
                return this.curvesB.Convert(this.clut.Convert(value));
            }

            if (this.bToA)
            {
                // ICC mBA order: B, matrix, M, CLUT, A.
                value = this.curvesB.Convert(value);
                value = this.hasMatrix ? this.matrix.Convert(value) : value;
                value = this.curvesM.Convert(value);
                value = this.hasClut ? this.clut.Convert(value) : value;
                return this.curvesA.Convert(value);
            }

            // ICC mAB order: A, CLUT, M, matrix, B.
            value = this.curvesA.Convert(value);
            value = this.hasClut ? this.clut.Convert(value) : value;
            value = this.curvesM.Convert(value);
            value = this.hasMatrix ? this.matrix.Convert(value) : value;
            return this.curvesB.Convert(value);
        }

        /// <inheritdoc/>
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Profile stage order is constant for the whole block. Curves, matrix and CLUT keep all colors in registers.
            if (this.legacy)
            {
                if (this.hasMatrix)
                {
                    this.matrix.Convert(ref x, ref y, ref z);
                }

                this.curvesA.Convert(ref x, ref y, ref z, ref w);
                this.clut.Convert(ref x, ref y, ref z, ref w);
                this.curvesB.Convert(ref x, ref y, ref z, ref w);
                return;
            }

            if (this.bToA)
            {
                this.curvesB.Convert(ref x, ref y, ref z, ref w);
                if (this.hasMatrix)
                {
                    this.matrix.Convert(ref x, ref y, ref z);
                }

                this.curvesM.Convert(ref x, ref y, ref z, ref w);
                if (this.hasClut)
                {
                    this.clut.Convert(ref x, ref y, ref z, ref w);
                }

                this.curvesA.Convert(ref x, ref y, ref z, ref w);
                return;
            }

            this.curvesA.Convert(ref x, ref y, ref z, ref w);
            if (this.hasClut)
            {
                this.clut.Convert(ref x, ref y, ref z, ref w);
            }

            this.curvesM.Convert(ref x, ref y, ref z, ref w);
            if (this.hasMatrix)
            {
                this.matrix.Convert(ref x, ref y, ref z);
            }

            this.curvesB.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Profile stage order is constant for the whole block. Curves, matrix and CLUT keep all colors in registers.
            if (this.legacy)
            {
                if (this.hasMatrix)
                {
                    this.matrix.Convert(ref x, ref y, ref z);
                }

                this.curvesA.Convert(ref x, ref y, ref z, ref w);
                this.clut.Convert(ref x, ref y, ref z, ref w);
                this.curvesB.Convert(ref x, ref y, ref z, ref w);
                return;
            }

            if (this.bToA)
            {
                this.curvesB.Convert(ref x, ref y, ref z, ref w);
                if (this.hasMatrix)
                {
                    this.matrix.Convert(ref x, ref y, ref z);
                }

                this.curvesM.Convert(ref x, ref y, ref z, ref w);
                if (this.hasClut)
                {
                    this.clut.Convert(ref x, ref y, ref z, ref w);
                }

                this.curvesA.Convert(ref x, ref y, ref z, ref w);
                return;
            }

            this.curvesA.Convert(ref x, ref y, ref z, ref w);
            if (this.hasClut)
            {
                this.clut.Convert(ref x, ref y, ref z, ref w);
            }

            this.curvesM.Convert(ref x, ref y, ref z, ref w);
            if (this.hasMatrix)
            {
                this.matrix.Convert(ref x, ref y, ref z);
            }

            this.curvesB.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Profile stage order is constant for the whole block. Curves, matrix and CLUT keep all colors in registers.
            if (this.legacy)
            {
                if (this.hasMatrix)
                {
                    this.matrix.Convert(ref x, ref y, ref z);
                }

                this.curvesA.Convert(ref x, ref y, ref z, ref w);
                this.clut.Convert(ref x, ref y, ref z, ref w);
                this.curvesB.Convert(ref x, ref y, ref z, ref w);
                return;
            }

            if (this.bToA)
            {
                this.curvesB.Convert(ref x, ref y, ref z, ref w);
                if (this.hasMatrix)
                {
                    this.matrix.Convert(ref x, ref y, ref z);
                }

                this.curvesM.Convert(ref x, ref y, ref z, ref w);
                if (this.hasClut)
                {
                    this.clut.Convert(ref x, ref y, ref z, ref w);
                }

                this.curvesA.Convert(ref x, ref y, ref z, ref w);
                return;
            }

            this.curvesA.Convert(ref x, ref y, ref z, ref w);
            if (this.hasClut)
            {
                this.clut.Convert(ref x, ref y, ref z, ref w);
            }

            this.curvesM.Convert(ref x, ref y, ref z, ref w);
            if (this.hasMatrix)
            {
                this.matrix.Convert(ref x, ref y, ref z);
            }

            this.curvesB.Convert(ref x, ref y, ref z, ref w);
        }
    }
}
