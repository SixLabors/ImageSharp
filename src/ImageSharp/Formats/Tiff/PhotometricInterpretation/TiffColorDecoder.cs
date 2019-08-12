// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// The base class for photometric interpretation decoders.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class TiffColorDecoder<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly uint[] bitsPerSample;

        private readonly uint[] colorMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffColorDecoder{TPixel}"/> class.
        /// </summary>
        /// <param name="bitsPerSample">The number of bits per sample for each pixel.</param>
        /// <param name="colorMap">The RGB color lookup table to use for decoding the image.</param>
        protected TiffColorDecoder(uint[] bitsPerSample, uint[] colorMap)
        {
            this.bitsPerSample = bitsPerSample;
            this.colorMap = colorMap;
        }

        /*
        /// <summary>
        /// Gets the photometric interpretation value.
        /// </summary>
        /// <value>
        /// The photometric interpretation value.
        /// </value>
        public TiffColorType ColorType { get; }
        */

        /// <summary>
        /// Decodes source raw pixel data using the current photometric interpretation.
        /// </summary>
        /// <param name="data">The buffer to read image data from.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract void Decode(byte[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height);
    }
}
