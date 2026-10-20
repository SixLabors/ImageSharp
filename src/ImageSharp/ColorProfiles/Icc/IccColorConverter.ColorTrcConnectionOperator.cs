// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Connects matrix/TRC profiles without temporary PCS scaling or separate adjacent matrix operations.
    /// </summary>
    /// <typeparam name="TSource">The source channel-curve operator selected during profile setup.</typeparam>
    /// <typeparam name="TTarget">The target channel-curve operator selected during profile setup.</typeparam>
    internal struct ColorTrcConnectionOperator<TSource, TTarget> : IIccColorConverterOperator
        where TSource : struct, IIccColorConverterOperator
        where TTarget : struct, IIccColorConverterOperator
    {
        // Writable generic fields avoid defensive struct copies during constrained calls. Conversion only reads their state.
        private TSource sourceCurves;
        private TTarget targetCurves;
        private readonly MatrixOperator matrix;
        private readonly MatrixOperator matrixAfterClipping;
        private readonly bool clipNegativeXyz;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorTrcConnectionOperator{TSource, TTarget}"/> struct.
        /// </summary>
        /// <param name="sourceCurves">The source profile's device curves.</param>
        /// <param name="sourceMatrix">The source matrix combined with the linear XYZ adjustment.</param>
        /// <param name="targetMatrix">The target profile's XYZ-to-device matrix.</param>
        /// <param name="targetCurves">The target profile's inverse device curves.</param>
        /// <param name="clipNegativeXyz">Whether the PCS adjustment clips negative XYZ before the target matrix.</param>
        public ColorTrcConnectionOperator(
            TSource sourceCurves,
            MatrixOperator sourceMatrix,
            MatrixOperator targetMatrix,
            TTarget targetCurves,
            bool clipNegativeXyz)
        {
            this.sourceCurves = sourceCurves;
            this.targetCurves = targetCurves;
            this.clipNegativeXyz = clipNegativeXyz;

            // A required XYZ clipping step prevents matrix concatenation across that boundary.
            // Otherwise one matrix maps source linear components directly to target linear components.
            this.matrix = clipNegativeXyz ? sourceMatrix : sourceMatrix.Then(targetMatrix);
            this.matrixAfterClipping = targetMatrix;
        }

        /// <inheritdoc/>
        public bool IsTrc => false;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            value = this.matrix.Convert(this.sourceCurves.Convert(value));
            if (this.clipNegativeXyz)
            {
                value = new Vector4(Vector3.Max(value.AsVector3(), Vector3.Zero), 1F);
                value = this.matrixAfterClipping.Convert(value);
            }

            value.W = 1F;
            return this.targetCurves.Convert(value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Each lane is one independent color. Curves keep their original order around the linear stages.
            this.sourceCurves.Convert(ref x, ref y, ref z, ref w);
            this.matrix.Convert(ref x, ref y, ref z);
            if (this.clipNegativeXyz)
            {
                // Black-point adjustment clips negative XYZ before the target matrix. Device RGB remains unbounded.
                x = Vector128.Max(x, Vector128<float>.Zero);
                y = Vector128.Max(y, Vector128<float>.Zero);
                z = Vector128.Max(z, Vector128<float>.Zero);
                this.matrixAfterClipping.Convert(ref x, ref y, ref z);
            }

            w = Vector128<float>.One;
            this.targetCurves.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Each lane is one independent color. Curves keep their original order around the linear stages.
            this.sourceCurves.Convert(ref x, ref y, ref z, ref w);
            this.matrix.Convert(ref x, ref y, ref z);
            if (this.clipNegativeXyz)
            {
                // Black-point adjustment clips negative XYZ before the target matrix. Device RGB remains unbounded.
                x = Vector256.Max(x, Vector256<float>.Zero);
                y = Vector256.Max(y, Vector256<float>.Zero);
                z = Vector256.Max(z, Vector256<float>.Zero);
                this.matrixAfterClipping.Convert(ref x, ref y, ref z);
            }

            w = Vector256<float>.One;
            this.targetCurves.Convert(ref x, ref y, ref z, ref w);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Each lane is one independent color. Curves keep their original order around the linear stages.
            this.sourceCurves.Convert(ref x, ref y, ref z, ref w);
            this.matrix.Convert(ref x, ref y, ref z);
            if (this.clipNegativeXyz)
            {
                // Black-point adjustment clips negative XYZ before the target matrix. Device RGB remains unbounded.
                x = Vector512.Max(x, Vector512<float>.Zero);
                y = Vector512.Max(y, Vector512<float>.Zero);
                z = Vector512.Max(z, Vector512<float>.Zero);
                this.matrixAfterClipping.Convert(ref x, ref y, ref z);
            }

            w = Vector512<float>.One;
            this.targetCurves.Convert(ref x, ref y, ref z, ref w);
        }
    }
}
