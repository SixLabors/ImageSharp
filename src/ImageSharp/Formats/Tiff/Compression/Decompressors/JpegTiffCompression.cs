// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed as a jpeg stream.
/// </summary>
internal class JpegTiffCompression : TiffBaseDecompressor
{
    private readonly JpegDecoderOptions options;

    private readonly byte[] jpegTables;

    private readonly TiffPhotometricInterpretation photometricInterpretation;

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegTiffCompression"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="options">The specialized jpeg decoder options.</param>
    /// <param name="jpegTables">The JPEG tables containing the quantization and/or Huffman tables.</param>
    /// <param name="photometricInterpretation">The photometric interpretation.</param>
    public JpegTiffCompression(
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        JpegDecoderOptions options,
        byte[] jpegTables,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(memoryAllocator, width, bitsPerPixel)
    {
        this.options = options;
        this.jpegTables = jpegTables;
        this.photometricInterpretation = photometricInterpretation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegTiffCompression"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="options">The specialized jpeg decoder options.</param>
    /// <param name="photometricInterpretation">The photometric interpretation.</param>
    public JpegTiffCompression(
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        JpegDecoderOptions options,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(memoryAllocator, width, bitsPerPixel)
    {
        this.options = options;
        this.photometricInterpretation = photometricInterpretation;
    }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        if (this.jpegTables != null)
        {
            this.DecodeJpegData(stream, buffer, true, cancellationToken);
        }
        else
        {
            using Image<Rgb24> image = Image.Load<Rgb24>(stream);
            CopyImageBytesToBuffer(buffer, image.Frames.RootFrame.PixelBuffer);
        }
    }

    protected void DecodeJpegData(BufferedReadStream stream, Span<byte> buffer, bool loadTables, CancellationToken cancellationToken)
    {
        using JpegDecoderCore jpegDecoder = new(this.options);
        Configuration configuration = this.options.GeneralOptions.Configuration;
        switch (this.photometricInterpretation)
        {
            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
            {
                using SpectralConverter<L8> spectralConverterGray = new GrayJpegSpectralConverter<L8>(configuration);
                HuffmanScanDecoder scanDecoderGray = new(stream, spectralConverterGray, cancellationToken);

                if (loadTables)
                {
                    jpegDecoder.LoadTables(this.jpegTables, scanDecoderGray);
                }

                jpegDecoder.ParseStream(stream, spectralConverterGray, cancellationToken);

                using Buffer2D<L8> decompressedBuffer = spectralConverterGray.GetPixelBuffer(cancellationToken);
                CopyImageBytesToBuffer(buffer, decompressedBuffer);
                break;
            }

            case TiffPhotometricInterpretation.YCbCr:
            case TiffPhotometricInterpretation.Rgb:
            {
                using SpectralConverter<Rgb24> spectralConverter = new TiffJpegSpectralConverter<Rgb24>(configuration, this.photometricInterpretation);
                HuffmanScanDecoder scanDecoder = new(stream, spectralConverter, cancellationToken);

                if (loadTables)
                {
                    jpegDecoder.LoadTables(this.jpegTables, scanDecoder);
                }

                jpegDecoder.ParseStream(stream, spectralConverter, cancellationToken);

                using Buffer2D<Rgb24> decompressedBuffer = spectralConverter.GetPixelBuffer(cancellationToken);
                CopyImageBytesToBuffer(buffer, decompressedBuffer);
                break;
            }

            default:
                TiffThrowHelper.ThrowNotSupported($"Jpeg compressed tiff with photometric interpretation {this.photometricInterpretation} is not supported");
                break;
        }
    }

    private static void CopyImageBytesToBuffer(Span<byte> buffer, Buffer2D<Rgb24> pixelBuffer)
    {
        int offset = 0;
        for (int y = 0; y < pixelBuffer.Height; y++)
        {
            Span<Rgb24> pixelRowSpan = pixelBuffer.DangerousGetRowSpan(y);
            Span<byte> rgbBytes = MemoryMarshal.AsBytes(pixelRowSpan);
            rgbBytes.CopyTo(buffer[offset..]);
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
            rgbBytes.CopyTo(buffer[offset..]);
            offset += rgbBytes.Length;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
