// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct HalfVector4P
{
    /// <summary>
    /// Provides bulk operations for associated half-precision pixels.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<HalfVector4P>
    {
        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.UnpackUnassociated(
                MemoryMarshal.Cast<HalfVector4P, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<HalfVector4P, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
            => this.ToAssociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.PackAssociated(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<HalfVector4P, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<HalfVector4P, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
            => this.FromUnassociatedVector4Destructive(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
            => this.FromAssociatedVector4Destructive(configuration, source, destination);
    }
}
