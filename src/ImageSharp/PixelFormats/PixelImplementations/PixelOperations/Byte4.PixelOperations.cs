// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Byte4
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    /// <remarks>
    /// Byte4 and Rgba32 have the same packed layout, so the shared RGBA byte conversions read and write Byte4 storage directly.
    /// Native Byte4 components are scaled components multiplied by 255. The native forms apply that range step in the same pass.
    /// </remarks>
    internal class PixelOperations : PixelOperations<Byte4>
    {
        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.ByteToNormalizedFloat<ToNativeOperator<PixelFormats.PixelOperations.NoAlphaOperator>>(
                MemoryMarshal.AsBytes(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.ByteToNormalizedFloat<ToNativeOperator<PixelFormats.PixelOperations.AssociateOperator>>(
                MemoryMarshal.AsBytes(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.AsBytes(source), MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.ByteToNormalizedFloat<PixelFormats.PixelOperations.AssociateOperator>(
                MemoryMarshal.AsBytes(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.NormalizedFloatToByteSaturate<ToScaledOperator<PixelFormats.PixelOperations.NoAlphaOperator>>(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.AsBytes(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.NormalizedFloatToByteSaturate<ToScaledOperator<PixelFormats.PixelOperations.UnassociateOperator>>(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.AsBytes(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.NormalizedFloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.AsBytes(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.NormalizedFloatToByteSaturate<PixelFormats.PixelOperations.UnassociateOperator>(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.AsBytes(destination[..source.Length]));
        }

        /// <summary>
        /// Applies <typeparamref name="TAlpha"/> to scaled components, then maps them to the native range with native = scaled * 255.
        /// </summary>
        /// <typeparam name="TAlpha">The alpha step for each scaled pixel.</typeparam>
        private readonly struct ToNativeOperator<TAlpha> : PixelFormats.PixelOperations.IAlphaOperator
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector128<float> Apply(Vector128<float> source)
                => Vector128.MultiplyAddEstimate(TAlpha.Apply(source), Vector128.Create((float)byte.MaxValue), Vector128<float>.Zero);

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector256<float> Apply(Vector256<float> source)
                => Vector256.MultiplyAddEstimate(TAlpha.Apply(source), Vector256.Create((float)byte.MaxValue), Vector256<float>.Zero);

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector512<float> Apply(Vector512<float> source)
                => Vector512.MultiplyAddEstimate(TAlpha.Apply(source), Vector512.Create((float)byte.MaxValue), Vector512<float>.Zero);
        }

        /// <summary>
        /// Maps native components to the scaled range with scaled = native / 255, then applies <typeparamref name="TAlpha"/>.
        /// </summary>
        /// <typeparam name="TAlpha">The alpha step for each scaled pixel.</typeparam>
        private readonly struct ToScaledOperator<TAlpha> : PixelFormats.PixelOperations.IAlphaOperator
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector128<float> Apply(Vector128<float> source)
                => TAlpha.Apply(source / Vector128.Create((float)byte.MaxValue));

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector256<float> Apply(Vector256<float> source)
                => TAlpha.Apply(source / Vector256.Create((float)byte.MaxValue));

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector512<float> Apply(Vector512<float> source)
                => TAlpha.Apply(source / Vector512.Create((float)byte.MaxValue));
        }
    }
}
