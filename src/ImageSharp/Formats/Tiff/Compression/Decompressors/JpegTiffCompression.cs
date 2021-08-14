// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed as a jpeg stream.
    /// </summary>
    internal class JpegTiffCompression : TiffBaseDecompressor
    {
        private readonly Configuration configuration;

        private readonly byte[] jpegTables;

        private readonly TiffPhotometricInterpretation photometricInterpretation;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegTiffCompression"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
        /// <param name="width">The image width.</param>
        /// <param name="bitsPerPixel">The bits per pixel.</param>
        /// <param name="jpegTables">The JPEG tables containing the quantization and/or Huffman tables.</param>
        /// <param name="photometricInterpretation">The photometric interpretation.</param>
        public JpegTiffCompression(
            Configuration configuration,
            MemoryAllocator memoryAllocator,
            int width,
            int bitsPerPixel,
            byte[] jpegTables,
            TiffPhotometricInterpretation photometricInterpretation)
            : base(memoryAllocator, width, bitsPerPixel)
        {
            this.configuration = configuration;
            this.jpegTables = jpegTables;
            this.photometricInterpretation = photometricInterpretation;
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer)
        {
            Image<Rgb24> image;
            if (this.jpegTables != null)
            {
                var jpegDecoder = new JpegDecoderCore(this.configuration, new JpegDecoder());

                // TODO: Should we pass through the CancellationToken from the tiff decoder?
                // If the PhotometricInterpretation is YCbCr we explicitly assume the JPEG data is in RGB color space.
                // There seems no other way to determine that the JPEG data is RGB colorspace (no APP14 marker, componentId's are not RGB).
                using SpectralConverter<Rgb24> spectralConverter = this.photometricInterpretation == TiffPhotometricInterpretation.YCbCr ?
                    new RgbJpegSpectralConverter<Rgb24>(this.configuration, CancellationToken.None) : new SpectralConverter<Rgb24>(this.configuration, CancellationToken.None);
                var scanDecoder = new HuffmanScanDecoder(stream, spectralConverter, CancellationToken.None);
                jpegDecoder.LoadTables(this.jpegTables, scanDecoder);
                scanDecoder.ResetInterval = 0;
                jpegDecoder.ParseStream(stream, scanDecoder, CancellationToken.None);

                image = new Image<Rgb24>(this.configuration, spectralConverter.PixelBuffer, new ImageMetadata());
            }
            else
            {
                image = Image.Load<Rgb24>(stream);
            }

            int offset = 0;
            for (int y = 0; y < image.Height; y++)
            {
                Span<Rgb24> pixelRowSpan = image.GetPixelRowSpan(y);
                Span<byte> rgbBytes = MemoryMarshal.AsBytes(pixelRowSpan);
                rgbBytes.CopyTo(buffer.Slice(offset));
                offset += rgbBytes.Length;
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
