// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

internal static partial class Vector4Converters
{
    /// <summary>
    /// Retains native IEEE components during bulk packing and expansion.
    /// </summary>
    public readonly struct IdentityOperator : TensorPrimitives_.IUnaryOperator, IStatefulVector4Operator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => value;
    }

    /// <summary>
    /// Bounds RGB to the working color range and alpha to [0,1].
    /// </summary>
    public readonly struct ColorClampOperator : IStatefulVector4Operator
    {
        private readonly Vector128<float> minimum;
        private readonly Vector128<float> maximum;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorClampOperator"/> struct.
        /// </summary>
        /// <param name="minimum">The minimum stored RGB component. The working color lower bound also applies.</param>
        /// <param name="maximum">The maximum stored RGB component. The working color upper bound also applies.</param>
        public ColorClampOperator(float minimum, float maximum)
        {
            // Color conversion uses one RGB range for float and half storage. Native vector conversion keeps its IEEE values.
            minimum = MathF.Max(minimum, ColorNumerics.MinColorValue);
            maximum = MathF.Min(maximum, ColorNumerics.MaxColorValue);

            // Each four-lane group is RGBA. Alpha represents opacity, so its bounds do not follow RGB storage precision.
            this.minimum = Vector128.Create(minimum, minimum, minimum, 0F);
            this.maximum = Vector128.Create(maximum, maximum, maximum, 1F);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 value) => this.Invoke(value.AsVector128()).AsVector4();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => ColorNumerics.ClampToFinite(value, this.minimum, this.maximum);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => ColorNumerics.ClampToFinite(value, Vector256.Create(this.minimum, this.minimum), Vector256.Create(this.maximum, this.maximum));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Repeat the RGBA bounds for all four pixels; no bounds depend on neighboring pixels.
            Vector256<float> lower = Vector256.Create(this.minimum, this.minimum);
            Vector256<float> upper = Vector256.Create(this.maximum, this.maximum);
            return ColorNumerics.ClampToFinite(value, Vector512.Create(lower, lower), Vector512.Create(upper, upper));
        }
    }

    /// <summary>
    /// Rounds finite opacity in [0,1] to the alpha value stored by a half-precision pixel.
    /// </summary>
    public readonly struct HalfAlphaOperator : TensorPrimitives_.IUnaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => HalfTypeHelper.Unpack(HalfTypeHelper.Pack(value));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Alpha is replicated across each pixel's lanes. Pack and expand it before multiplying RGB,
            // so association uses exactly the alpha that the destination can store.
            return HalfTypeHelper.Unpack(HalfTypeHelper.Pack(value, value)).Lower;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            return HalfTypeHelper.Unpack(HalfTypeHelper.Pack(value, value)).Lower;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            return HalfTypeHelper.Unpack(HalfTypeHelper.Pack(value, value)).Lower;
        }
    }

    /// <summary>
    /// Associates RGB with alpha and bounds the stored result in the same SIMD pass.
    /// </summary>
    /// <typeparam name="TAlphaOperator">The opacity operation applied before RGB multiplication.</typeparam>
    public readonly struct AssociateOperator<TAlphaOperator> : IStatefulVector4Operator
        where TAlphaOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        private readonly TAlphaOperator alphaOperation;
        private readonly ColorClampOperator clamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssociateOperator{TAlphaOperator}"/> struct.
        /// </summary>
        /// <param name="alphaOperation">The operation applied to alpha before multiplication.</param>
        /// <param name="clamp">The destination component bounds.</param>
        public AssociateOperator(TAlphaOperator alphaOperation, ColorClampOperator clamp)
        {
            this.alphaOperation = alphaOperation;
            this.clamp = clamp;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 value) => this.Invoke(value.AsVector128()).AsVector4();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Bound RGB and opacity before multiplication. Since alpha is in [0,1], association cannot overflow finite RGB.
            value = this.clamp.Invoke(value);

            // Each RGBA group repeats W. Restore W after multiplication so alpha is not squared.
            Vector128<float> alpha = this.alphaOperation.Invoke(Vector128_.ShuffleNative(value, 0b_11_11_11_11));
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            return Vector128.ConditionalSelect(alphaMask, alpha, value * alpha);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Bound RGB and opacity before multiplication. Since alpha is in [0,1], association cannot overflow finite RGB.
            value = this.clamp.Invoke(value);

            // Each RGBA group repeats W. Restore W after multiplication so alpha is not squared.
            Vector256<float> alpha = this.alphaOperation.Invoke(Vector256_.ShuffleNative(value, 0b_11_11_11_11));
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector256.ConditionalSelect(alphaMask, alpha, value * alpha);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Bound RGB and opacity before multiplication. Since alpha is in [0,1], association cannot overflow finite RGB.
            value = this.clamp.Invoke(value);

            // Each RGBA group repeats W. Restore W after multiplication so alpha is not squared.
            Vector512<float> alpha = this.alphaOperation.Invoke(Vector512_.ShuffleNative(value, 0b_11_11_11_11));
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alphaMask, alpha, value * alpha);
        }
    }

    /// <summary>
    /// Retains the straight color when associated input alpha is bounded or rounded for storage.
    /// </summary>
    /// <typeparam name="TAlphaOperator">The rounding operation for bounded opacity.</typeparam>
    public readonly struct ReassociateOperator<TAlphaOperator> : IStatefulVector4Operator
        where TAlphaOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        private readonly TAlphaOperator alphaOperation;
        private readonly ColorClampOperator clamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReassociateOperator{TAlphaOperator}"/> struct.
        /// </summary>
        /// <param name="alphaOperation">The storage rounding applied to opacity in [0,1].</param>
        /// <param name="clamp">The destination RGB and opacity bounds.</param>
        public ReassociateOperator(TAlphaOperator alphaOperation, ColorClampOperator clamp)
        {
            this.alphaOperation = alphaOperation;
            this.clamp = clamp;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 value) => this.Invoke(value.AsVector128()).AsVector4();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Make the divisor finite before division. Retain positive finite alpha above one so RGB keeps its original meaning.
            // NaN and negative alpha become zero; positive infinity becomes float.MaxValue. Bound stored opacity separately.
            Vector128<float> alpha = Vector128_.ShuffleNative(value, 0b_11_11_11_11);
            Vector128<float> zero = Vector128<float>.Zero;
            alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector128.Create(float.MaxValue));
            Vector128<float> storedAlpha = this.alphaOperation.Invoke(ColorNumerics.ClampToFinite(alpha, zero, Vector128<float>.One));
            Vector128<float> unchanged = Vector128.Equals(alpha, storedAlpha);
            if (Vector128.EqualsAll(unchanged.AsInt32(), Vector128.Create(-1)))
            {
                // Valid associated input needs no RGB arithmetic. Keep its original bits and zero-alpha color data.
                return this.clamp.Invoke(value);
            }

            // Divide before multiplying by the stored alpha. Do not clamp the straight intermediate: it can exceed
            // the destination range even when the final associated value fits. Unchanged pixels keep their original RGB.
            Vector128<float> result = Numerics.UnPremultiply(value, alpha) * storedAlpha;
            result = Vector128.ConditionalSelect(unchanged, value, result);
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            return this.clamp.Invoke(Vector128.ConditionalSelect(alphaMask, storedAlpha, result));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Make the divisor finite before division. Retain positive finite alpha above one so RGB keeps its original meaning.
            // NaN and negative alpha become zero; positive infinity becomes float.MaxValue. Bound stored opacity separately.
            Vector256<float> alpha = Vector256_.ShuffleNative(value, 0b_11_11_11_11);
            Vector256<float> zero = Vector256<float>.Zero;
            alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector256.Create(float.MaxValue));
            Vector256<float> storedAlpha = this.alphaOperation.Invoke(ColorNumerics.ClampToFinite(alpha, zero, Vector256<float>.One));
            Vector256<float> unchanged = Vector256.Equals(alpha, storedAlpha);
            if (Vector256.EqualsAll(unchanged.AsInt32(), Vector256.Create(-1)))
            {
                // Valid associated input needs no RGB arithmetic. Keep its original bits and zero-alpha color data.
                return this.clamp.Invoke(value);
            }

            // Divide before multiplying by the stored alpha. Do not clamp the straight intermediate: it can exceed
            // the destination range even when the final associated value fits. Unchanged pixels keep their original RGB.
            Vector256<float> result = Numerics.UnPremultiply(value, alpha) * storedAlpha;
            result = Vector256.ConditionalSelect(unchanged, value, result);
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return this.clamp.Invoke(Vector256.ConditionalSelect(alphaMask, storedAlpha, result));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Make the divisor finite before division. Retain positive finite alpha above one so RGB keeps its original meaning.
            // NaN and negative alpha become zero; positive infinity becomes float.MaxValue. Bound stored opacity separately.
            Vector512<float> alpha = Vector512_.ShuffleNative(value, 0b_11_11_11_11);
            Vector512<float> zero = Vector512<float>.Zero;
            alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector512.Create(float.MaxValue));
            Vector512<float> storedAlpha = this.alphaOperation.Invoke(ColorNumerics.ClampToFinite(alpha, zero, Vector512<float>.One));
            Vector512<float> unchanged = Vector512.Equals(alpha, storedAlpha);
            if (Vector512.EqualsAll(unchanged.AsInt32(), Vector512.Create(-1)))
            {
                // Valid associated input needs no RGB arithmetic. Keep its original bits and zero-alpha color data.
                return this.clamp.Invoke(value);
            }

            // Divide before multiplying by the stored alpha. Do not clamp the straight intermediate: it can exceed
            // the destination range even when the final associated value fits. Unchanged pixels keep their original RGB.
            Vector512<float> result = Numerics.UnPremultiply(value, alpha) * storedAlpha;
            result = Vector512.ConditionalSelect(unchanged, value, result);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return this.clamp.Invoke(Vector512.ConditionalSelect(alphaMask, storedAlpha, result));
        }
    }

    /// <summary>
    /// Unassociates RGB and bounds the color components before writing the destination.
    /// </summary>
    public readonly struct UnassociateOperator : IStatefulVector4Operator
    {
        private readonly ColorClampOperator clamp;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnassociateOperator"/> struct.
        /// </summary>
        /// <param name="clamp">The destination component bounds.</param>
        public UnassociateOperator(ColorClampOperator clamp) => this.clamp = clamp;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Invoke(Vector4 value) => this.Invoke(value.AsVector128()).AsVector4();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
        {
            // Use a finite divisor while retaining positive finite alpha above one. Clamping it to one here would change RGB.
            // Zero retains stored RGB; positive infinity uses float.MaxValue. Bound output opacity after division.
            Vector128<float> alpha = Vector128_.ShuffleNative(value, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, Vector128<float>.Zero, Vector128.Create(float.MaxValue));

            return this.clamp.Invoke(Numerics.UnPremultiply(value, alpha));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
        {
            // Use a finite divisor while retaining positive finite alpha above one. Clamping it to one here would change RGB.
            // Zero retains stored RGB; positive infinity uses float.MaxValue. Bound output opacity after division.
            Vector256<float> alpha = Vector256_.ShuffleNative(value, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, Vector256<float>.Zero, Vector256.Create(float.MaxValue));

            return this.clamp.Invoke(Numerics.UnPremultiply(value, alpha));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
        {
            // Use a finite divisor while retaining positive finite alpha above one. Clamping it to one here would change RGB.
            // Zero retains stored RGB; positive infinity uses float.MaxValue. Bound output opacity after division.
            Vector512<float> alpha = Vector512_.ShuffleNative(value, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, Vector512<float>.Zero, Vector512.Create(float.MaxValue));

            return this.clamp.Invoke(Numerics.UnPremultiply(value, alpha));
        }
    }
}
