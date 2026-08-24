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
        // Protected hooks are same-instance extension points. Cross-format reuse must request the representation through public modifier dispatch.
        private const PixelConversionModifiers UnassociatedScaledModifiers = PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply;
        private const PixelConversionModifiers AssociatedScaledModifiers = PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply;

        private static readonly Vector4 NativeMaximum = new(byte.MaxValue);

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Byte4 and Rgba32 have the same packed layout. Reusing the optimized Rgba32 unpacker avoids four scalar byte
            // extractions per pixel; the affine transform then restores Byte4's native [0, 255] vector range.
            PixelOperations<Rgba32>.Instance.ToVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination, UnassociatedScaledModifiers);
            Vector4Converters.MultiplyThenAdd(destination, NativeMaximum, Vector4.Zero);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Association is performed in normalized color space before mapping all four components back to Byte4's native range.
            PixelOperations<Rgba32>.Instance.ToVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination, AssociatedScaledModifiers);
            Vector4Converters.MultiplyThenAdd(destination, NativeMaximum, Vector4.Zero);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelOperations<Rgba32>.Instance.ToVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination, UnassociatedScaledModifiers);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Byte4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            PixelOperations<Rgba32>.Instance.ToVector4(configuration, MemoryMarshal.Cast<Byte4, Rgba32>(source), destination, AssociatedScaledModifiers);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Vector4Converters.AddThenDivide(source, Vector4.Zero, NativeMaximum);
            PixelOperations<Rgba32>.Instance.FromVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination), UnassociatedScaledModifiers);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Native alpha is normalized with RGB so the Rgba32 bulk path can unassociate in a single, consistent coordinate space.
            Vector4Converters.AddThenDivide(source, Vector4.Zero, NativeMaximum);
            PixelOperations<Rgba32>.Instance.FromVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination), AssociatedScaledModifiers);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            PixelOperations<Rgba32>.Instance.FromVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination), UnassociatedScaledModifiers);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Byte4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            PixelOperations<Rgba32>.Instance.FromVector4Destructive(configuration, source, MemoryMarshal.Cast<Byte4, Rgba32>(destination), AssociatedScaledModifiers);
        }
    }
}
