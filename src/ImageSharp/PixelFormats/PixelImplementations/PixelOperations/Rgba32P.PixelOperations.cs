// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Rgba32P
{
    /// <summary>
    /// Provides optimized bulk operations for <see cref="Rgba32P"/>.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<Rgba32P>
    {
        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
            => Vector4Converters.AssociatedRgbaCompatible.ToUnassociatedVector4(source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
            => Vector4Converters.AssociatedRgbaCompatible.ToAssociatedVector4(source, destination);

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination)
        {
            // Native and scaled vectors have the same range for this format, so the byte-specialized converter is valid for both contracts.
            Vector4Converters.AssociatedRgbaCompatible.FromUnassociatedVector4(source, destination);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination)
        {
            // The converter rescales RGB when alpha rounds so the channels remain associated with the byte alpha actually stored.
            Vector4Converters.AssociatedRgbaCompatible.FromAssociatedVector4(source, destination);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
            => Vector4Converters.AssociatedRgbaCompatible.ToUnassociatedVector4(source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
        {
            // This byte format has the same normalized native and scaled ranges, so the unmodified converter satisfies both contracts.
            Vector4Converters.AssociatedRgbaCompatible.ToAssociatedVector4(source, destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination)
        {
            Vector4Converters.AssociatedRgbaCompatible.FromUnassociatedVector4(source, destination);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination)
        {
            Vector4Converters.AssociatedRgbaCompatible.FromAssociatedVector4(source, destination);
        }
    }
}
