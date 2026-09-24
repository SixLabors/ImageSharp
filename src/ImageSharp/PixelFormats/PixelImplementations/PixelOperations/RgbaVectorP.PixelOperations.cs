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
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            MemoryMarshal.Cast<RgbaVectorP, Vector4>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            MemoryMarshal.Cast<RgbaVectorP, Vector4>(source).CopyTo(destination);

            // Unassociate the destination after copying so the stored source components are unchanged.
            Numerics.UnPremultiply(destination[..source.Length]);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            MemoryMarshal.Cast<Vector4, RgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // The source is destructive by contract, so associate it before copying to storage.
            Numerics.Premultiply(source);
            MemoryMarshal.Cast<Vector4, RgbaVectorP>(source).CopyTo(destination);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
            => this.ToAssociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaVectorP> source, Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
            => this.FromAssociatedVector4Destructive(configuration, source, destination);

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaVectorP> destination)
            => this.FromUnassociatedVector4Destructive(configuration, source, destination);
    }
}
