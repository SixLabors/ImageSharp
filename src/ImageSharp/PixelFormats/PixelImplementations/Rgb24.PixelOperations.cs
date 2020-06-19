// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rgb24
    {
        /// <summary>
        /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="Rgb24"/>.
        /// </summary>
        internal partial class PixelOperations : PixelOperations<Rgb24>
        {
            /// <inheritdoc />
            public override void PackFromRgbPlanes(
                Configuration configuration,
                ReadOnlySpan<byte> redChannel,
                ReadOnlySpan<byte> greenChannel,
                ReadOnlySpan<byte> blueChannel,
                Span<Rgb24> destination)
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.IsTrue(redChannel.Length == greenChannel.Length, nameof(redChannel), "Red channel must be same size as green channel");
                Guard.IsTrue(greenChannel.Length == blueChannel.Length, nameof(greenChannel), "Green channel must be same size as blue channel");
                Guard.DestinationShouldNotBeTooShort(redChannel, destination, nameof(destination));

                destination = destination.Slice(0, redChannel.Length);

                SimdUtils.PackBytesToUInt32SaturateChannel4(redChannel, greenChannel, blueChannel, MemoryMarshal.AsBytes(destination));
            }
        }
    }
}
