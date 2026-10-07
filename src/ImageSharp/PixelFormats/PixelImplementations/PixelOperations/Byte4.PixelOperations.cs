// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Byte4
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<Byte4>
    {
        private static readonly Vector4 NativeMaximum = new(byte.MaxValue);

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Byte4 and Rgba32 have the same packed layout. Reusing the optimized Rgba32 unpacker avoids four scalar byte
            // extractions per pixel; the affine transform then restores Byte4's native [0, 255] vector range.
            PixelOperations<Rgba32>.Instance.ToUnassociatedScaledVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeMaximum, Vector4.Zero);
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Association is performed in normalized color space before mapping all four components back to Byte4's native range.
            PixelOperations<Rgba32>.Instance.ToAssociatedScaledVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeMaximum, Vector4.Zero);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelOperations<Rgba32>.Instance.ToUnassociatedScaledVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination);
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelOperations<Rgba32>.Instance.ToAssociatedScaledVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination);
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Vector4Converters.AddThenDivide(source, Vector4.Zero, NativeMaximum);
            PixelOperations<Rgba32>.Instance.FromUnassociatedScaledVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination));
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Native alpha is normalized with RGB so the Rgba32 bulk path can unassociate in a single, consistent coordinate space.
            Vector4Converters.AddThenDivide(source, Vector4.Zero, NativeMaximum);
            PixelOperations<Rgba32>.Instance.FromAssociatedScaledVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination));
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            PixelOperations<Rgba32>.Instance.FromUnassociatedScaledVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination));
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            PixelOperations<Rgba32>.Instance.FromAssociatedScaledVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination));
        }
    }
}
