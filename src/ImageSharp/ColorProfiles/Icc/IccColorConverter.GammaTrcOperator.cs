// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Applies three positive finite gamma curves selected before component traversal.
    /// </summary>
    internal readonly struct GammaTrcOperator : IIccColorConverterOperator
    {
        private readonly TensorPrimitives_.PowOperator power0;
        private readonly TensorPrimitives_.PowOperator power1;
        private readonly TensorPrimitives_.PowOperator power2;
        private readonly Vector3 signMask;

        /// <summary>
        /// Initializes a new instance of the <see cref="GammaTrcOperator"/> struct.
        /// </summary>
        /// <param name="x">The first channel's classified power.</param>
        /// <param name="y">The second channel's classified power.</param>
        /// <param name="z">The third channel's classified power.</param>
        public GammaTrcOperator(TensorPrimitives_.PowOperator x, TensorPrimitives_.PowOperator y, TensorPrimitives_.PowOperator z)
            : this(x, y, z, Vector3.Zero)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GammaTrcOperator"/> struct with configured channel domains.
        /// </summary>
        /// <param name="x">The first channel's classified power.</param>
        /// <param name="y">The second channel's classified power.</param>
        /// <param name="z">The third channel's classified power.</param>
        /// <param name="signMask">The analytic sign-bit mask for each channel, or zero for the original domain.</param>
        public GammaTrcOperator(
            TensorPrimitives_.PowOperator x,
            TensorPrimitives_.PowOperator y,
            TensorPrimitives_.PowOperator z,
            Vector3 signMask)
        {
            this.power0 = x;
            this.power1 = y;
            this.power2 = z;
            this.signMask = signMask;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            // Select magnitudes only for enabled analytic channels. The original domain keeps its lower bound.
            // The fourth component is not part of the RGB curves and has no sign mask.
            Vector4 sign = value & new Vector4(this.signMask, 0F);
            value ^= sign;
            value.X = this.power0.Invoke(value.X < 0F ? 0F : value.X);
            value.Y = this.power1.Invoke(value.Y < 0F ? 0F : value.Y);
            value.Z = this.power2.Invoke(value.Z < 0F ? 0F : value.Z);
            return value | sign;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Each lane is one color. Setup selected the sign domain without a per-lane policy branch.
            Vector128<float> signX = x & Vector128.Create(this.signMask.X);
            Vector128<float> signY = y & Vector128.Create(this.signMask.Y);
            Vector128<float> signZ = z & Vector128.Create(this.signMask.Z);
            x ^= signX;
            y ^= signY;
            z ^= signZ;

            // Setup selected finite positive exponents. Each register contains one channel across 4 colors.
            x = ConvertGamma(x, this.power0);
            y = ConvertGamma(y, this.power1);
            z = ConvertGamma(z, this.power2);
            x |= signX;
            y |= signY;
            z |= signZ;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Each lane is one color. Setup selected the sign domain without a per-lane policy branch.
            Vector256<float> signX = x & Vector256.Create(this.signMask.X);
            Vector256<float> signY = y & Vector256.Create(this.signMask.Y);
            Vector256<float> signZ = z & Vector256.Create(this.signMask.Z);
            x ^= signX;
            y ^= signY;
            z ^= signZ;

            // Setup selected finite positive exponents. Each register contains one channel across 8 colors.
            x = ConvertGamma(x, this.power0);
            y = ConvertGamma(y, this.power1);
            z = ConvertGamma(z, this.power2);
            x |= signX;
            y |= signY;
            z |= signZ;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Each lane is one color. Setup selected the sign domain without a per-lane policy branch.
            Vector512<float> signX = x & Vector512.Create(this.signMask.X);
            Vector512<float> signY = y & Vector512.Create(this.signMask.Y);
            Vector512<float> signZ = z & Vector512.Create(this.signMask.Z);
            x ^= signX;
            y ^= signY;
            z ^= signZ;

            // Setup selected finite positive exponents. Each register contains one channel across 16 colors.
            x = ConvertGamma(x, this.power0);
            y = ConvertGamma(y, this.power1);
            z = ConvertGamma(z, this.power2);
            x |= signX;
            y |= signY;
            z |= signZ;
        }

        /// <summary>
        /// Applies one positive finite gamma without repeating general exponent classification.
        /// </summary>
        /// <param name="value">The component values in independent color lanes.</param>
        /// <param name="power">The exponent and sign mask selected during gamma-stage setup.</param>
        /// <returns>The converted components, including the existing IEEE special values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ConvertGamma(Vector128<float> value, TensorPrimitives_.PowOperator power)
        {
            // ICC gamma curves bound negative values at zero. Abs changes only negative zero after that selection.
            // The precomputed odd-exponent mask restores its sign, while fractional powers retain positive zero.
            value = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128<float>.Zero), Vector128<float>.Zero, value);
            Vector128<float> result = Vector128_.Pow(Vector128.Abs(value), power.Exponent);
            return Vector128.ConditionalSelect(Vector128.Create(power.OddMask).AsSingle(), Vector128.CopySign(result, value), result);
        }

        /// <summary>
        /// Applies one positive finite gamma without repeating general exponent classification.
        /// </summary>
        /// <param name="value">The component values in independent color lanes.</param>
        /// <param name="power">The exponent and sign mask selected during gamma-stage setup.</param>
        /// <returns>The converted components, including the existing IEEE special values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ConvertGamma(Vector256<float> value, TensorPrimitives_.PowOperator power)
        {
            // ICC gamma curves bound negative values at zero. Abs changes only negative zero after that selection.
            // The precomputed odd-exponent mask restores its sign, while fractional powers retain positive zero.
            value = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256<float>.Zero), Vector256<float>.Zero, value);
            Vector256<float> result = Vector256_.Pow(Vector256.Abs(value), power.Exponent);
            return Vector256.ConditionalSelect(Vector256.Create(power.OddMask).AsSingle(), Vector256.CopySign(result, value), result);
        }

        /// <summary>
        /// Applies one positive finite gamma without repeating general exponent classification.
        /// </summary>
        /// <param name="value">The component values in independent color lanes.</param>
        /// <param name="power">The exponent and sign mask selected during gamma-stage setup.</param>
        /// <returns>The converted components, including the existing IEEE special values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ConvertGamma(Vector512<float> value, TensorPrimitives_.PowOperator power)
        {
            // ICC gamma curves bound negative values at zero. Abs changes only negative zero after that selection.
            // The precomputed odd-exponent mask restores its sign, while fractional powers retain positive zero.
            value = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512<float>.Zero), Vector512<float>.Zero, value);
            Vector512<float> result = Vector512_.Pow(Vector512.Abs(value), power.Exponent);
            return Vector512.ConditionalSelect(Vector512.Create(power.OddMask).AsSingle(), Vector512.CopySign(result, value), result);
        }
    }
}
