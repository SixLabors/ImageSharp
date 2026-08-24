// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct HalfVector4P
{
    /// <summary>
    /// Provides optimized bulk operations for <see cref="HalfVector4P"/>.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<HalfVector4P>
    {
        private static readonly Vector4 NativeToScaledMultiplier = new(HalfTypeHelper.InverseFiniteRange);
        private static readonly Vector4 NativeToScaledOffset = new(HalfTypeHelper.ScaledMidpoint);
        private static readonly Vector4 ScaledToNativeMultiplier = new(HalfTypeHelper.FiniteRange);
        private static readonly Vector4 ScaledToNativeOffset = new(HalfTypeHelper.FiniteMinimum);

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            this.ToUnassociatedScaledVector4(configuration, source, destination);
            Vector4Converters.MultiplyThenAdd(destination[..source.Length], ScaledToNativeMultiplier, ScaledToNativeOffset);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<HalfVector4P, RgbaHalfP>(source), destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            this.ToAssociatedScaledVector4(configuration, source, destination);
            Numerics.UnPremultiply(destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<HalfVector4P, RgbaHalfP>(source), destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeToScaledMultiplier, NativeToScaledOffset);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Vector4Converters.MultiplyThenAdd(source, NativeToScaledMultiplier, NativeToScaledOffset);
            Associate(source);
            PackAssociatedScaled(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Vector4Converters.MultiplyThenAdd(source, NativeToScaledMultiplier, NativeToScaledOffset);
            Reassociate(source);
            PackAssociatedScaled(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Associate(source);
            PackAssociatedScaled(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Reassociate(source);
            PackAssociatedScaled(source, destination[..source.Length]);
        }

        /// <summary>
        /// Associates scaled vectors with the alpha values representable by native binary16 storage.
        /// </summary>
        /// <param name="source">The vectors to convert in place.</param>
        private static void Associate(Span<Vector4> source)
        {
            ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorsPerRegister = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - vectorsPerRegister; i += vectorsPerRegister)
                {
                    ref Vector512<float> vector = ref Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    vector = Associate(vector);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorsPerRegister = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - vectorsPerRegister; i += vectorsPerRegister)
                {
                    ref Vector256<float> vector = ref Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    vector = Associate(vector);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    ref Vector128<float> vector = ref Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    vector = Associate(vector);
                }

                return;
            }

            for (; i < source.Length; i++)
            {
                Unsafe.Add(ref sourceBase, (uint)i) = HalfVector4P.Associate(Unsafe.Add(ref sourceBase, (uint)i));
            }
        }

        /// <summary>
        /// Reassociates scaled vectors with the alpha values representable by native binary16 storage.
        /// </summary>
        /// <param name="source">The vectors to convert in place.</param>
        private static void Reassociate(Span<Vector4> source)
        {
            ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int vectorsPerRegister = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - vectorsPerRegister; i += vectorsPerRegister)
                {
                    ref Vector512<float> vector = ref Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    vector = Reassociate(vector);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int vectorsPerRegister = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - vectorsPerRegister; i += vectorsPerRegister)
                {
                    ref Vector256<float> vector = ref Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    vector = Reassociate(vector);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    ref Vector128<float> vector = ref Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    vector = Reassociate(vector);
                }

                return;
            }

            for (; i < source.Length; i++)
            {
                Unsafe.Add(ref sourceBase, (uint)i) = HalfVector4P.Reassociate(Unsafe.Add(ref sourceBase, (uint)i));
            }
        }

        /// <summary>
        /// Converts an unassociated scaled vector to associated scaled components.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Associate(Vector128<float> source)
        {
            source = ClampUnit(source);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> storedAlpha = QuantizeScaledAlpha(alpha);
            Vector128<float> result = source * storedAlpha;
            return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), storedAlpha, result);
        }

        /// <summary>
        /// Converts unassociated scaled vectors to associated scaled components.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Associate(Vector256<float> source)
        {
            source = ClampUnit(source);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> storedAlpha = QuantizeScaledAlpha(alpha);
            Vector256<float> result = source * storedAlpha;
            return Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), storedAlpha, result);
        }

        /// <summary>
        /// Converts unassociated scaled vectors to associated scaled components.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Associate(Vector512<float> source)
        {
            source = ClampUnit(source);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> storedAlpha = QuantizeScaledAlpha(alpha);
            Vector512<float> result = source * storedAlpha;
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
        }

        /// <summary>
        /// Reassociates an associated scaled vector after alpha quantization.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The reassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Reassociate(Vector128<float> source)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> storedAlpha = QuantizeScaledAlpha(alpha);
            Vector128<float> result = source * (storedAlpha / alpha);
            result = Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), storedAlpha, result);
            result = Vector128.Min(Vector128.Max(result, zero), storedAlpha);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Reassociates associated scaled vectors after alpha quantization.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The reassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Reassociate(Vector256<float> source)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> storedAlpha = QuantizeScaledAlpha(alpha);
            Vector256<float> result = source * (storedAlpha / alpha);
            result = Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), storedAlpha, result);
            result = Vector256.Min(Vector256.Max(result, zero), storedAlpha);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Reassociates associated scaled vectors after alpha quantization.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <returns>The reassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Reassociate(Vector512<float> source)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> storedAlpha = QuantizeScaledAlpha(alpha);
            Vector512<float> result = source * (storedAlpha / alpha);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector512.Min(Vector512.Max(result, zero), storedAlpha);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Quantizes scaled alpha through the native binary16 representation.
        /// </summary>
        /// <param name="alpha">The scaled alpha lanes.</param>
        /// <returns>The scaled alpha values represented by binary16 storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> QuantizeScaledAlpha(Vector128<float> alpha)
        {
            Vector128<float> native = (ClampUnit(alpha) * Vector128.Create(HalfTypeHelper.FiniteRange)) + Vector128.Create(HalfTypeHelper.FiniteMinimum);
            return (HalfTypeHelper.RoundToHalf(native) * Vector128.Create(HalfTypeHelper.InverseFiniteRange)) + Vector128.Create(HalfTypeHelper.ScaledMidpoint);
        }

        /// <summary>
        /// Quantizes scaled alpha through the native binary16 representation.
        /// </summary>
        /// <param name="alpha">The scaled alpha lanes.</param>
        /// <returns>The scaled alpha values represented by binary16 storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> QuantizeScaledAlpha(Vector256<float> alpha)
        {
            Vector256<float> native = (ClampUnit(alpha) * Vector256.Create(HalfTypeHelper.FiniteRange)) + Vector256.Create(HalfTypeHelper.FiniteMinimum);
            return (HalfTypeHelper.RoundToHalf(native) * Vector256.Create(HalfTypeHelper.InverseFiniteRange)) + Vector256.Create(HalfTypeHelper.ScaledMidpoint);
        }

        /// <summary>
        /// Quantizes scaled alpha through the native binary16 representation.
        /// </summary>
        /// <param name="alpha">The scaled alpha lanes.</param>
        /// <returns>The scaled alpha values represented by binary16 storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> QuantizeScaledAlpha(Vector512<float> alpha)
        {
            Vector512<float> native = (ClampUnit(alpha) * Vector512.Create(HalfTypeHelper.FiniteRange)) + Vector512.Create(HalfTypeHelper.FiniteMinimum);
            return (HalfTypeHelper.RoundToHalf(native) * Vector512.Create(HalfTypeHelper.InverseFiniteRange)) + Vector512.Create(HalfTypeHelper.ScaledMidpoint);
        }

        /// <summary>
        /// Clamps vectors to the scaled color range while preserving NaN lanes.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ClampUnit(Vector128<float> source)
        {
            Vector128<float> clamped = Vector128.Min(Vector128.Max(source, Vector128<float>.Zero), Vector128<float>.One);
            return Vector128.ConditionalSelect(Vector128.Equals(source, source), clamped, source);
        }

        /// <summary>
        /// Clamps vectors to the scaled color range while preserving NaN lanes.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ClampUnit(Vector256<float> source)
        {
            Vector256<float> clamped = Vector256.Min(Vector256.Max(source, Vector256<float>.Zero), Vector256<float>.One);
            return Vector256.ConditionalSelect(Vector256.Equals(source, source), clamped, source);
        }

        /// <summary>
        /// Clamps vectors to the scaled color range while preserving NaN lanes.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ClampUnit(Vector512<float> source)
        {
            Vector512<float> clamped = Vector512.Min(Vector512.Max(source, Vector512<float>.Zero), Vector512<float>.One);
            return Vector512.ConditionalSelect(Vector512.Equals(source, source), clamped, source);
        }

        /// <summary>
        /// Maps associated scaled vectors to native components and packs them as binary16 values.
        /// </summary>
        /// <param name="source">The associated scaled vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        private static void PackAssociatedScaled(Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Vector4Converters.MultiplyThenAdd(source, ScaledToNativeMultiplier, ScaledToNativeOffset);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4P, RgbaHalfP>(destination));
        }
    }
}
