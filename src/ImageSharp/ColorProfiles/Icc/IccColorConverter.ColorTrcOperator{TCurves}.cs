// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Applies selected channel curves and the profile's XYZ matrix in the required order.
    /// </summary>
    /// <typeparam name="TCurves">The channel-curve formula selected during profile setup.</typeparam>
    internal struct ColorTrcOperator<TCurves> : IIccColorConverterOperator
        where TCurves : struct, IIccColorConverterOperator
    {
        // A writable generic field avoids defensive copies at constrained calls. The configured curves do not mutate their state.
        private TCurves curves;
        private readonly MatrixOperator matrix;
        private readonly bool toPcs;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorTrcOperator{TCurves}"/> struct.
        /// </summary>
        /// <param name="matrix">The RGB-to-XYZ matrix or its inverse.</param>
        /// <param name="curves">The selected forward or inverse channel curves.</param>
        /// <param name="toPcs">Whether the profile maps device RGB to encoded PCS XYZ.</param>
        public ColorTrcOperator(MatrixOperator matrix, TCurves curves, bool toPcs)
        {
            this.matrix = matrix;
            this.curves = curves;
            this.toPcs = toPcs;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <summary>
        /// Gets the channel curves for connection setup.
        /// </summary>
        public TCurves Curves => this.curves;

        /// <summary>
        /// Gets the profile matrix for connection setup.
        /// </summary>
        public MatrixOperator Matrix => this.matrix;

        /// <summary>
        /// Gets a value indicating whether the profile maps device RGB to encoded PCS XYZ.
        /// </summary>
        public bool ToPcs => this.toPcs;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            if (this.toPcs)
            {
                value = this.matrix.Convert(this.curves.Convert(value));
                return new CieXyz(value.AsVector3()).ToScaledVector4();
            }

            value = this.matrix.Convert(value);
            value = new Vector4(CieXyz.FromScaledVector4(value).AsVector3Unsafe(), 1F);
            return this.curves.Convert(value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Each lane is one color. Apply curves before the RGB-to-XYZ matrix and after the XYZ-to-RGB matrix.
            if (this.toPcs)
            {
                this.curves.Convert(ref x, ref y, ref z, ref w);
                this.matrix.Convert(ref x, ref y, ref z);
                x *= Vector128.Create(PcsXyzEncodeScale);
                y *= Vector128.Create(PcsXyzEncodeScale);
                z *= Vector128.Create(PcsXyzEncodeScale);
                w = Vector128<float>.One;
            }
            else
            {
                this.matrix.Convert(ref x, ref y, ref z);
                x *= Vector128.Create(PcsXyzDecodeScale);
                y *= Vector128.Create(PcsXyzDecodeScale);
                z *= Vector128.Create(PcsXyzDecodeScale);
                w = Vector128<float>.One;
                this.curves.Convert(ref x, ref y, ref z, ref w);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Each lane is one color. Apply curves before the RGB-to-XYZ matrix and after the XYZ-to-RGB matrix.
            if (this.toPcs)
            {
                this.curves.Convert(ref x, ref y, ref z, ref w);
                this.matrix.Convert(ref x, ref y, ref z);
                x *= Vector256.Create(PcsXyzEncodeScale);
                y *= Vector256.Create(PcsXyzEncodeScale);
                z *= Vector256.Create(PcsXyzEncodeScale);
                w = Vector256<float>.One;
            }
            else
            {
                this.matrix.Convert(ref x, ref y, ref z);
                x *= Vector256.Create(PcsXyzDecodeScale);
                y *= Vector256.Create(PcsXyzDecodeScale);
                z *= Vector256.Create(PcsXyzDecodeScale);
                w = Vector256<float>.One;
                this.curves.Convert(ref x, ref y, ref z, ref w);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Each lane is one color. Apply curves before the RGB-to-XYZ matrix and after the XYZ-to-RGB matrix.
            if (this.toPcs)
            {
                this.curves.Convert(ref x, ref y, ref z, ref w);
                this.matrix.Convert(ref x, ref y, ref z);
                x *= Vector512.Create(PcsXyzEncodeScale);
                y *= Vector512.Create(PcsXyzEncodeScale);
                z *= Vector512.Create(PcsXyzEncodeScale);
                w = Vector512<float>.One;
            }
            else
            {
                this.matrix.Convert(ref x, ref y, ref z);
                x *= Vector512.Create(PcsXyzDecodeScale);
                y *= Vector512.Create(PcsXyzDecodeScale);
                z *= Vector512.Create(PcsXyzDecodeScale);
                w = Vector512<float>.One;
                this.curves.Convert(ref x, ref y, ref z, ref w);
            }
        }
    }
}
