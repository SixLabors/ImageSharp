// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct Short4
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal class PixelOperations : PixelOperations<Short4>
    {
        private static readonly Vector4 NativeOffset = new(-MinNeg);
        private static readonly Vector4 NativeMagnitude = new(Range);

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Short4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<Short4, short>(source), destination[..source.Length], false, false);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Short4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Signed integer components use an affine native range. Expand to scaled vectors, associate there, then restore the
            // native zero point so RGB and alpha remain in the same coordinate system.
            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<Short4, short>(source), destination, false, true);
            Numerics.Premultiply(destination);
            Vector4Converters.MultiplyThenAdd(destination, NativeMagnitude, -NativeOffset);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Short4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<Short4, short>(source), destination[..source.Length], false, true);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Short4> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SignedShort4PixelOperations.ToVector4(MemoryMarshal.Cast<Short4, short>(source), destination, false, true);
            Numerics.Premultiply(destination);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Short4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<Short4, short>(destination), false, false);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Short4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            // Normalize all four components before unassociating, then use the scaled packing path to preserve Short4's affine mapping.
            Vector4Converters.AddThenDivide(source, NativeOffset, NativeMagnitude);
            Numerics.UnPremultiply(source);
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<Short4, short>(destination), false, true);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Short4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<Short4, short>(destination), false, true);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Short4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Numerics.UnPremultiply(source);
            SignedShort4PixelOperations.FromVector4(source, MemoryMarshal.Cast<Short4, short>(destination), false, true);
        }
    }
}
