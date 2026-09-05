// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

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
            HalfTypeHelper.ToScaled(destination);
            Numerics.Premultiply(destination);
            HalfTypeHelper.FromScaled(destination);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            RgbaHalfP.PixelOperations.Unpack(MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(source), destination);
            HalfTypeHelper.ToScaled(destination);
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
            HalfTypeHelper.ToScaled(source);
            Numerics.UnPremultiply(source);
            HalfTypeHelper.FromScaled(source);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.FromScaled(source);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Numerics.UnPremultiply(source);
            HalfTypeHelper.FromScaled(source);
            RgbaHalfP.PixelOperations.PackUnclamped(source, MemoryMarshal.Cast<HalfVector4, RgbaHalfP>(destination[..source.Length]));
        }
    }
}
