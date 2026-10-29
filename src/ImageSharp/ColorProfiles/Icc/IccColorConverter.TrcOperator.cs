// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Applies the profile's channel curves without interface dispatch for each component of each color.
    /// </summary>
    internal readonly struct TrcOperator : IIccColorConverterOperator
    {
        private readonly CurveOperator curve0;
        private readonly CurveOperator curve1;
        private readonly CurveOperator curve2;
        private readonly CurveOperator curve3;
        private readonly int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrcOperator"/> struct.
        /// </summary>
        /// <param name="entries">The curves for up to four input channels.</param>
        /// <param name="inverted">Whether each channel uses the inverse of its curve.</param>
        public TrcOperator(ReadOnlySpan<IccTagDataEntry> entries, bool inverted)
        {
            if (entries.Length > 4)
            {
                throw new InvalidIccProfileException("ICC conversion supports at most four tone response curves.");
            }

            this.count = entries.Length;

            this.curve0 = entries.Length > 0 ? new CurveOperator(entries[0], inverted) : default;
            this.curve1 = entries.Length > 1 ? new CurveOperator(entries[1], inverted) : default;
            this.curve2 = entries.Length > 2 ? new CurveOperator(entries[2], inverted) : default;
            this.curve3 = entries.Length > 3 ? new CurveOperator(entries[3], inverted) : default;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <summary>
        /// Builds an sRGB-form stage when all three channel curves use that formula.
        /// </summary>
        /// <param name="srgb">The selected curves with their original profile coefficients.</param>
        /// <returns>Whether every channel uses the normalized sRGB formula.</returns>
        public bool TryGetSRgb(out SRgbTrcOperator srgb)
        {
            if (this.count == 3 && this.curve0.TryGetSRgb(out SRgbCurveOperator x) && this.curve1.TryGetSRgb(out SRgbCurveOperator y)
                && this.curve2.TryGetSRgb(out SRgbCurveOperator z))
            {
                srgb = new(x, y, z);
                return true;
            }

            srgb = default;
            return false;
        }

        /// <summary>
        /// Builds a gamma-only stage when all three channel curves contain positive finite exponents.
        /// </summary>
        /// <param name="gamma">The configured gamma stage.</param>
        /// <returns>Whether every channel can use the gamma-only stage.</returns>
        public bool TryGetGamma(out GammaTrcOperator gamma)
        {
            // This is profile-shape selection at setup. Identity, sampled and other parametric curves retain their own formulas.
            if (this.count == 3 && this.curve0.TryGetGamma(out TensorPrimitives_.PowOperator x)
                && this.curve1.TryGetGamma(out TensorPrimitives_.PowOperator y) && this.curve2.TryGetGamma(out TensorPrimitives_.PowOperator z))
            {
                gamma = new GammaTrcOperator(x, y, z);
                return true;
            }

            gamma = default;
            return false;
        }

        /// <summary>
        /// Applies the configured curves to one color, retaining components that have no curve.
        /// </summary>
        /// <param name="value">The input components.</param>
        /// <returns>The converted components.</returns>
        public Vector4 Convert(Vector4 value)
        {
            value.X = this.curve0.Convert(value.X);
            if (this.count > 1)
            {
                value.Y = this.curve1.Convert(value.Y);
            }

            if (this.count > 2)
            {
                value.Z = this.curve2.Convert(value.Z);
            }

            if (this.count > 3)
            {
                value.W = this.curve3.Convert(value.W);
            }

            return value;
        }

        /// <summary>
        /// Applies the configured curves to 4 colors held in component registers.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // The channel count is fixed by the profile. Each call processes a complete SIMD block, with no channel-object dispatch.
            x = this.curve0.Convert(x);
            if (this.count > 1)
            {
                y = this.curve1.Convert(y);
            }

            if (this.count > 2)
            {
                z = this.curve2.Convert(z);
            }

            if (this.count > 3)
            {
                w = this.curve3.Convert(w);
            }
        }

        /// <summary>
        /// Applies the configured curves to 8 colors held in component registers.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // The channel count is fixed by the profile. Each call processes a complete SIMD block, with no channel-object dispatch.
            x = this.curve0.Convert(x);
            if (this.count > 1)
            {
                y = this.curve1.Convert(y);
            }

            if (this.count > 2)
            {
                z = this.curve2.Convert(z);
            }

            if (this.count > 3)
            {
                w = this.curve3.Convert(w);
            }
        }

        /// <summary>
        /// Applies the configured curves to 16 colors held in component registers.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // The channel count is fixed by the profile. Each call processes a complete SIMD block, with no channel-object dispatch.
            x = this.curve0.Convert(x);
            if (this.count > 1)
            {
                y = this.curve1.Convert(y);
            }

            if (this.count > 2)
            {
                z = this.curve2.Convert(z);
            }

            if (this.count > 3)
            {
                w = this.curve3.Convert(w);
            }
        }
    }
}
