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
public partial struct RgbaHalfP
{
    /// <summary>
    /// Provides bulk operations for associated half-precision pixels.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<RgbaHalfP>
    {
        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.UnpackUnassociated(
                MemoryMarshal.Cast<RgbaHalfP, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<RgbaHalfP, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<RgbaHalfP, ushort>(source),
                destination[..source.Length],
                new Vector4Converters.UnassociateOperator(new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<RgbaHalfP> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<RgbaHalfP, ushort>(source),
                destination[..source.Length],
                new Vector4Converters.ReassociateOperator<Vector4Converters.IdentityOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.PackAssociated(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<RgbaHalfP, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<RgbaHalfP, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Pack(
                source,
                MemoryMarshal.Cast<RgbaHalfP, ushort>(destination[..source.Length]),
                new Vector4Converters.AssociateOperator<Vector4Converters.HalfAlphaOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<RgbaHalfP> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Pack(
                source,
                MemoryMarshal.Cast<RgbaHalfP, ushort>(destination[..source.Length]),
                new Vector4Converters.ReassociateOperator<Vector4Converters.HalfAlphaOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }
    }
}
