// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rgb24
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal partial class PixelOperations : PixelOperations<Rgb24>
        {
            private static readonly Lazy<PixelTypeInfo> LazyInfo =
                new Lazy<PixelTypeInfo>(() => PixelTypeInfo.Create<Rgb24>(PixelAlphaRepresentation.None), true);

            /// <inheritdoc />
            public override PixelTypeInfo GetPixelTypeInfo() => LazyInfo.Value;

            /// <inheritdoc />
            internal override void PackFromRgbPlanes(
                Configuration configuration,
                ReadOnlySpan<byte> redChannel,
                ReadOnlySpan<byte> greenChannel,
                ReadOnlySpan<byte> blueChannel,
                Span<Rgb24> destination)
            {
                Guard.NotNull(configuration, nameof(configuration));
                int count = redChannel.Length;
                Guard.IsTrue(greenChannel.Length == count, nameof(greenChannel), "Channels must be of same size!");
                Guard.IsTrue(blueChannel.Length == count, nameof(blueChannel), "Channels must be of same size!");
                Guard.IsTrue(destination.Length > count + 2, nameof(destination), "'destination' must contain a padding of 3 elements!");

                SimdUtils.PackFromRgbPlanes(configuration, redChannel, greenChannel, blueChannel, destination);
            }
        }
    }
}
