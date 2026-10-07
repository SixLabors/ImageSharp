// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides bulk conversion for associated binary32 pixels.
/// </content>
public partial struct RgbaVectorP
{
    /// <summary>
    /// Bulk conversion for associated binary32 pixels.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<RgbaVectorP>
    {
        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            MemoryMarshal.Cast<RgbaVectorP, Vector4>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            MemoryMarshal.Cast<RgbaVectorP, Vector4>(source).CopyTo(destination);

            // Unassociate the destination after copying so the stored source components are unchanged.
            Numerics.UnPremultiply(destination[..source.Length]);
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            MemoryMarshal.Cast<Vector4, RgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // The source is destructive by contract, so associate it before copying to storage.
            Numerics.Premultiply(source);
            MemoryMarshal.Cast<Vector4, RgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            this.ToAssociatedVector4(configuration, source, destination);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            this.ToUnassociatedVector4(configuration, source, destination);
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            this.FromAssociatedVector4Destructive(configuration, source, destination);
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            this.FromUnassociatedVector4Destructive(configuration, source, destination);
        }
    }
}
