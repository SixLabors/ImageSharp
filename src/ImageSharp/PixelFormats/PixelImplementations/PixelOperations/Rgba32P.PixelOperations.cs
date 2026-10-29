// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Selects the shared storage-family implementation.
/// </content>
public partial struct Rgba32P
{
    /// <summary>
    /// Inherits the shared bulk conversions for this pixel's storage.
    /// </summary>
    internal partial class PixelOperations : RgbaCompatiblePixelOperations<Rgba32P>
    {
        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.AsBytes(source), MemoryMarshal.Cast<Vector4, float>(destination[..source.Length]));
        }

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            RgbaCompatiblePixelOperations<Rgba32P>.UnassociateBytes(MemoryMarshal.AsBytes(source), destination[..source.Length]);
        }
    }
}
