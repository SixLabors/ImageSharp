// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    // TODO: docs
    internal abstract class SpectralConverter<TPixel> : SpectralConverter
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Gets converted pixel buffer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Pixel buffer.</returns>
        public abstract Buffer2D<TPixel> GetPixelBuffer(CancellationToken cancellationToken);
    }
}
