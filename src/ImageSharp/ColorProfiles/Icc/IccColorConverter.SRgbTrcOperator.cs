// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Applies three sRGB-form curves selected during profile setup.
    /// </summary>
    internal readonly struct SRgbTrcOperator : IIccColorConverterOperator
    {
        private readonly SRgbCurveOperator curve0;
        private readonly SRgbCurveOperator curve1;
        private readonly SRgbCurveOperator curve2;

        /// <summary>
        /// Initializes a new instance of the <see cref="SRgbTrcOperator"/> struct.
        /// </summary>
        /// <param name="x">The normalized sRGB-form curve for the first channel.</param>
        /// <param name="y">The curve for the second channel.</param>
        /// <param name="z">The curve for the third channel.</param>
        public SRgbTrcOperator(SRgbCurveOperator x, SRgbCurveOperator y, SRgbCurveOperator z)
        {
            this.curve0 = x;
            this.curve1 = y;
            this.curve2 = z;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            value.X = this.curve0.Invoke(value.X);
            value.Y = this.curve1.Invoke(value.Y);
            value.Z = this.curve2.Invoke(value.Z);
            return value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Setup selected this formula for every channel. Keep each profile's coefficients and fourth component.
            x = this.curve0.Invoke(x);
            y = this.curve1.Invoke(y);
            z = this.curve2.Invoke(z);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Setup selected this formula for every channel. Keep each profile's coefficients and fourth component.
            x = this.curve0.Invoke(x);
            y = this.curve1.Invoke(y);
            z = this.curve2.Invoke(z);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Setup selected this formula for every channel. Keep each profile's coefficients and fourth component.
            x = this.curve0.Invoke(x);
            y = this.curve1.Invoke(y);
            z = this.curve2.Invoke(z);
        }
    }
}
