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
public partial struct HalfVector4P
{
    /// <summary>
    /// Provides bulk operations for associated half-precision pixels.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<HalfVector4P>
    {
        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.UnpackUnassociated(
                MemoryMarshal.Cast<HalfVector4P, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<HalfVector4P, ushort>(source),
                MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<HalfVector4P, ushort>(source),
                destination[..source.Length],
                new Vector4Converters.UnassociateOperator(new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<HalfVector4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Unpack(
                MemoryMarshal.Cast<HalfVector4P, ushort>(source),
                destination[..source.Length],
                new Vector4Converters.ReassociateOperator<Vector4Converters.IdentityOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.PackAssociated(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<HalfVector4P, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            HalfTypeHelper.Pack(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<HalfVector4P, ushort>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Pack(
                source,
                MemoryMarshal.Cast<HalfVector4P, ushort>(destination[..source.Length]),
                new Vector4Converters.AssociateOperator<Vector4Converters.HalfAlphaOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<HalfVector4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            // Convert and enforce RGB storage bounds and alpha [0,1] in the same pass.
            HalfTypeHelper.Pack(
                source,
                MemoryMarshal.Cast<HalfVector4P, ushort>(destination[..source.Length]),
                new Vector4Converters.ReassociateOperator<Vector4Converters.HalfAlphaOperator>(
                    default,
                    new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)));
        }
    }
}
