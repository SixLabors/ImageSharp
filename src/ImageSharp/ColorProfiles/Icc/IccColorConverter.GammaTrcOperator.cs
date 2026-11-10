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

        /// <summary>
        /// Initializes a new instance of the <see cref="GammaTrcOperator"/> struct.
        /// </summary>
        /// <param name="x">The first channel's classified power.</param>
        /// <param name="y">The second channel's classified power.</param>
        /// <param name="z">The third channel's classified power.</param>
        public GammaTrcOperator(TensorPrimitives_.PowOperator x, TensorPrimitives_.PowOperator y, TensorPrimitives_.PowOperator z)
        {
            this.power0 = x;
            this.power1 = y;
            this.power2 = z;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            // ICC gamma curves limit negative device values to zero. NaNs and values above one retain the existing rules.
            value.X = this.power0.Invoke(value.X < 0F ? 0F : value.X);
            value.Y = this.power1.Invoke(value.Y < 0F ? 0F : value.Y);
            value.Z = this.power2.Invoke(value.Z < 0F ? 0F : value.Z);
            return value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Setup selected finite positive exponents. Each register contains one channel across 4 colors.
            x = ConvertGamma(x, this.power0);
            y = ConvertGamma(y, this.power1);
            z = ConvertGamma(z, this.power2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Setup selected finite positive exponents. Each register contains one channel across 8 colors.
            x = ConvertGamma(x, this.power0);
            y = ConvertGamma(y, this.power1);
            z = ConvertGamma(z, this.power2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Setup selected finite positive exponents. Each register contains one channel across 16 colors.
            x = ConvertGamma(x, this.power0);
            y = ConvertGamma(y, this.power1);
            z = ConvertGamma(z, this.power2);
        }

        /// <summary>
        /// Applies one positive finite gamma without repeating general exponent classification.
        /// </summary>
        /// <param name="value">The component values in independent color lanes.</param>
        /// <param name="power">The exponent selected during gamma-stage setup.</param>
        /// <returns>The converted components, including the existing IEEE special values.</returns>
        // This 128-bit stage is not inlined. With six power evaluations inlined into one loop, the .NET 10 JIT keeps
        // most 128-bit temporaries of a profile connection on the stack: 682 stack accesses, against 54 with three.
        // With one call for each channel, a 1280 x 853 image takes 14.6 ms with SSE only, against 20.4 ms inlined.
        // The wider stages are inlined, because their registers hold the full chain.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Vector128<float> ConvertGamma(Vector128<float> value, TensorPrimitives_.PowOperator power)
        {
            // ICC gamma curves bound negative values at zero. The power kernel requires bases that are zero or more.
            value = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128<float>.Zero), Vector128<float>.Zero, value);
            return Vector128_.Pow(value, power.Exponent);
        }

        /// <summary>
        /// Applies one positive finite gamma without repeating general exponent classification.
        /// </summary>
        /// <param name="value">The component values in independent color lanes.</param>
        /// <param name="power">The exponent selected during gamma-stage setup.</param>
        /// <returns>The converted components, including the existing IEEE special values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ConvertGamma(Vector256<float> value, TensorPrimitives_.PowOperator power)
        {
            // ICC gamma curves bound negative values at zero. The power kernel requires bases that are zero or more.
            value = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256<float>.Zero), Vector256<float>.Zero, value);
            return Vector256_.Pow(value, power.Exponent);
        }

        /// <summary>
        /// Applies one positive finite gamma without repeating general exponent classification.
        /// </summary>
        /// <param name="value">The component values in independent color lanes.</param>
        /// <param name="power">The exponent selected during gamma-stage setup.</param>
        /// <returns>The converted components, including the existing IEEE special values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ConvertGamma(Vector512<float> value, TensorPrimitives_.PowOperator power)
        {
            // ICC gamma curves bound negative values at zero. The power kernel requires bases that are zero or more.
            value = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512<float>.Zero), Vector512<float>.Zero, value);
            return Vector512_.Pow(value, power.Exponent);
        }
    }
}
