// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is compressed as a jpeg stream.
    /// </summary>
    internal sealed class JpegTiffCompression : TiffBaseDecompressor
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
        protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer)
        {
            if (this.jpegTables != null)
            {
                using var jpegDecoder = new JpegDecoderCore(this.configuration, new JpegDecoder());

                switch (this.photometricInterpretation)
                {
                    case TiffPhotometricInterpretation.BlackIsZero:
                    case TiffPhotometricInterpretation.WhiteIsZero:
                    {
                        using SpectralConverter<L8> spectralConverterGray =
                            new GrayJpegSpectralConverter<L8>(this.configuration);
                        var scanDecoderGray = new HuffmanScanDecoder(stream, spectralConverterGray, CancellationToken.None);
                        jpegDecoder.LoadTables(this.jpegTables, scanDecoderGray);
                        jpegDecoder.ParseStream(stream, spectralConverterGray, CancellationToken.None);

                        // TODO: Should we pass through the CancellationToken from the tiff decoder?
                        using Buffer2D<L8> decompressedBuffer = spectralConverterGray.GetPixelBuffer(CancellationToken.None);
                        CopyImageBytesToBuffer(buffer, decompressedBuffer);
                        break;
                    }

                    case TiffPhotometricInterpretation.YCbCr:
                    case TiffPhotometricInterpretation.Rgb:
                    {
                        using SpectralConverter<Rgb24> spectralConverter =
                            new TiffJpegSpectralConverter<Rgb24>(this.configuration, this.photometricInterpretation);
                        var scanDecoder = new HuffmanScanDecoder(stream, spectralConverter, CancellationToken.None);
                        jpegDecoder.LoadTables(this.jpegTables, scanDecoder);
                        jpegDecoder.ParseStream(stream, spectralConverter, CancellationToken.None);

                        // TODO: Should we pass through the CancellationToken from the tiff decoder?
                        using Buffer2D<Rgb24> decompressedBuffer = spectralConverter.GetPixelBuffer(CancellationToken.None);
                        CopyImageBytesToBuffer(buffer, decompressedBuffer);
                        break;
                    }

                    default:
                        TiffThrowHelper.ThrowNotSupported($"Jpeg compressed tiff with photometric interpretation {this.photometricInterpretation} is not supported");
                        break;
                }
            }
            else
            {
                using var image = Image.Load<Rgb24>(stream);
                CopyImageBytesToBuffer(buffer, image.Frames.RootFrame.PixelBuffer);
            }
        }

        private static void CopyImageBytesToBuffer(Span<byte> buffer, Buffer2D<Rgb24> pixelBuffer)
        {
            int offset = 0;
            for (int y = 0; y < pixelBuffer.Height; y++)
            {
                Span<Rgb24> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
                Span<byte> rgbBytes = MemoryMarshal.AsBytes(pixelRowSpan);
                rgbBytes.CopyTo(buffer.Slice(offset));
                offset += rgbBytes.Length;
            }
        }

        private static void CopyImageBytesToBuffer(Span<byte> buffer, Buffer2D<L8> pixelBuffer)
        {
            int offset = 0;
            for (int y = 0; y < pixelBuffer.Height; y++)
            {
                Span<L8> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
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
