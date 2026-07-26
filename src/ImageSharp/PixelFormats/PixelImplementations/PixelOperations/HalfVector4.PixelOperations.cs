// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct HalfVector4
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<HalfVector4>
    {
        private static readonly Vector4 NativeToScaledMultiplier = new(HalfTypeHelper.InverseFiniteRange);
        private static readonly Vector4 NativeToScaledOffset = new(HalfTypeHelper.ScaledMidpoint);
        private static readonly Vector4 ScaledToNativeMultiplier = new(HalfTypeHelper.FiniteRange);
        private static readonly Vector4 ScaledToNativeOffset = new(HalfTypeHelper.FiniteMinimum);

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // The half-vector layouts are identical, so the shared expansion kernel can process the source without copying it.
            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(source), destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Association uses normalized opacity, not the native binary16 alpha value.
            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(source), destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeToScaledMultiplier, NativeToScaledOffset);
            Numerics.Premultiply(destination);
            Vector4Converters.MultiplyThenAdd(destination, ScaledToNativeMultiplier, ScaledToNativeOffset);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(source), destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeToScaledMultiplier, NativeToScaledOffset);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4> source, Span<Vector4> destination)
        {
            this.ToUnassociatedScaledVector4(configuration, source, destination);
            Numerics.Premultiply(destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // DirectX half vectors are not normalized formats, so values outside the nominal color range must reach storage unchanged.
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Restore normalized opacity before unassociating, then return the result to the native binary16 range.
            Vector4Converters.MultiplyThenAdd(source, NativeToScaledMultiplier, NativeToScaledOffset);
            Numerics.UnPremultiply(source);
            Vector4Converters.MultiplyThenAdd(source, ScaledToNativeMultiplier, ScaledToNativeOffset);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Vector4Converters.MultiplyThenAdd(source, ScaledToNativeMultiplier, ScaledToNativeOffset);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Numerics.UnPremultiply(source);
            Vector4Converters.MultiplyThenAdd(source, ScaledToNativeMultiplier, ScaledToNativeOffset);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }
    }
}
