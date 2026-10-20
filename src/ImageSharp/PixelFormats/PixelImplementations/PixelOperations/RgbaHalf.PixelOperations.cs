// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct RgbaHalf
{
    /// <summary>
    /// Provides optimized bulk operations for <see cref="RgbaHalf"/>.
    /// </summary>
    internal class PixelOperations : PixelOperations<RgbaHalf>
    {
        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Unpack(MemoryMarshal.Cast<RgbaHalf, ushort>(source), MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Association is fused with half expansion so the conversion reads and writes each span once.
            HalfTypeHelper.UnpackAssociated(MemoryMarshal.Cast<RgbaHalf, ushort>(source), MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<RgbaHalf, ushort>(source),
                destination[..source.Length],
                new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue));
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalf> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<RgbaHalf, ushort>(source),
                destination[..source.Length],
                new Vector4Converters.AssociateOperator<Vector4Converters.IdentityOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Pack(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.Cast<RgbaHalf, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Unassociation is fused with half-precision packing so processors do not pay for another pass over their vector buffer.
            HalfTypeHelper.PackFromAssociated(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.Cast<RgbaHalf, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Pack(
                source,
                MemoryMarshal.Cast<RgbaHalf, ushort>(destination[..source.Length]),
                new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue));
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalf> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Pack(
                source,
                MemoryMarshal.Cast<RgbaHalf, ushort>(destination[..source.Length]),
                new Vector4Converters.UnassociateOperator(new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }
    }
}
