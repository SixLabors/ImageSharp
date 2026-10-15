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
    /// Converts RGB through the profile's channel curves and XYZ matrix.
    /// </summary>
    internal readonly struct ColorTrcOperator : IIccColorConverterOperator
    {
        private readonly TrcOperator curves;
        private readonly MatrixOperator matrix;
        private readonly bool toPcs;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorTrcOperator"/> struct.
        /// </summary>
        /// <param name="red">The red XYZ matrix column.</param>
        /// <param name="green">The green XYZ matrix column.</param>
        /// <param name="blue">The blue XYZ matrix column.</param>
        /// <param name="curves">The red, green and blue channel curves.</param>
        /// <param name="toPcs">Whether to convert device RGB to encoded PCS XYZ.</param>
        public ColorTrcOperator(Vector3 red, Vector3 green, Vector3 blue, ReadOnlySpan<IccTagDataEntry> curves, bool toPcs)
        {
            this.toPcs = toPcs;
            this.curves = new TrcOperator(curves, !toPcs);
            Matrix4x4 matrix = new(red.X, red.Y, red.Z, 0F, green.X, green.Y, green.Z, 0F, blue.X, blue.Y, blue.Z, 0F, 0F, 0F, 0F, 1F);
            if (!toPcs)
            {
                Matrix4x4.Invert(matrix, out matrix);
            }

            this.matrix = new MatrixOperator(matrix, Vector3.Zero);
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

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
            // Each lane is one color. Keep the original curve/matrix order and ICC XYZ encoding scale.
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
            // Each lane is one color. Keep the original curve/matrix order and ICC XYZ encoding scale.
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
            // Each lane is one color. Keep the original curve/matrix order and ICC XYZ encoding scale.
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
