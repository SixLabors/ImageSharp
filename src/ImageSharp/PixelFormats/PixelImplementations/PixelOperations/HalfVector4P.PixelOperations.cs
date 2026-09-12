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
        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            this.ToUnassociatedScaledVector4(configuration, source, destination);
            HalfTypeHelper.FromScaled(destination[..source.Length]);
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
            HalfTypeHelper.ToScaled(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.ToScaled(source);
            Associate(source);
            PackAssociatedScaled(source, destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.ToScaled(source);
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

            // Clamp after the alpha ratio, matching the scalar conversion for nonfinite RGB.
            result = Numerics.Clamp(result, zero, storedAlpha);
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

            // Clamp after the alpha ratio, matching the scalar conversion for nonfinite RGB.
            result = Numerics.Clamp(result, zero, storedAlpha);
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

            // Clamp after the alpha ratio, matching the scalar conversion for nonfinite RGB.
            result = Numerics.Clamp(result, zero, storedAlpha);
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
            Vector128<float> native = HalfTypeHelper.FromScaled(alpha);
            return HalfTypeHelper.ToScaled(HalfTypeHelper.RoundToHalf(native));
        }

        /// <summary>
        /// Quantizes scaled alpha through the native binary16 representation.
        /// </summary>
        /// <param name="alpha">The scaled alpha lanes.</param>
        /// <returns>The scaled alpha values represented by binary16 storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> QuantizeScaledAlpha(Vector256<float> alpha)
        {
            Vector256<float> native = HalfTypeHelper.FromScaled(alpha);
            return HalfTypeHelper.ToScaled(HalfTypeHelper.RoundToHalf(native));
        }

        /// <summary>
        /// Quantizes scaled alpha through the native binary16 representation.
        /// </summary>
        /// <param name="alpha">The scaled alpha lanes.</param>
        /// <returns>The scaled alpha values represented by binary16 storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> QuantizeScaledAlpha(Vector512<float> alpha)
        {
            Vector512<float> native = HalfTypeHelper.FromScaled(alpha);
            return HalfTypeHelper.ToScaled(HalfTypeHelper.RoundToHalf(native));
        }

        /// <summary>
        /// Clamps vectors to the scaled color range, mapping NaN lanes to zero.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ClampUnit(Vector128<float> source) => Numerics.Clamp(source, Vector128<float>.Zero, Vector128<float>.One);

        /// <summary>
        /// Clamps vectors to the scaled color range, mapping NaN lanes to zero.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ClampUnit(Vector256<float> source) => Numerics.Clamp(source, Vector256<float>.Zero, Vector256<float>.One);

        /// <summary>
        /// Clamps vectors to the scaled color range, mapping NaN lanes to zero.
        /// </summary>
        /// <param name="source">The vectors to clamp.</param>
        /// <returns>The clamped vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ClampUnit(Vector512<float> source) => Numerics.Clamp(source, Vector512<float>.Zero, Vector512<float>.One);

        /// <summary>
        /// Maps associated scaled vectors to native components and packs them as binary16 values.
        /// </summary>
        /// <param name="source">The associated scaled vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        private static void PackAssociatedScaled(Span<Vector4> source, Span<HalfVector4P> destination)
        {
            HalfTypeHelper.FromScaled(source);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4P, RgbaHalfP>(destination));
        }
    }
}
