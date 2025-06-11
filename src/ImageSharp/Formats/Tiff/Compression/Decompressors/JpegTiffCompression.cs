// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Class to handle cases where TIFF image data is compressed as a jpeg stream.
/// </summary>
internal sealed class JpegTiffCompression : TiffBaseDecompressor
{
    private readonly JpegDecoderOptions options;

    private readonly byte[] jpegTables;

    private readonly TiffPhotometricInterpretation photometricInterpretation;

    private readonly ImageFrameMetadata metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegTiffCompression"/> class.
    /// </summary>
    /// <param name="options">The specialized jpeg decoder options.</param>
    /// <param name="memoryAllocator">The memoryAllocator to use for buffer allocations.</param>
    /// <param name="width">The image width.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="metadata">The image frame metadata.</param>
    /// <param name="jpegTables">The JPEG tables containing the quantization and/or Huffman tables.</param>
    /// <param name="photometricInterpretation">The photometric interpretation.</param>
    public JpegTiffCompression(
        JpegDecoderOptions options,
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        ImageFrameMetadata metadata,
        byte[] jpegTables,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(memoryAllocator, width, bitsPerPixel)
    {
        this.options = options;
        this.metadata = metadata;
        this.jpegTables = jpegTables;
        this.photometricInterpretation = photometricInterpretation;
    }

    /// <inheritdoc/>
    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        if (this.jpegTables != null)
        {
            this.DecodeJpegData(stream, buffer, cancellationToken);
        }
        else
        {
            using Image<Rgb24> image = Image.Load<Rgb24>(this.options.GeneralOptions, stream);
            JpegCompressionUtils.CopyImageBytesToBuffer(this.options.GeneralOptions.Configuration, buffer, image.Frames.RootFrame.PixelBuffer);
        }
    }

    private void DecodeJpegData(BufferedReadStream stream, Span<byte> buffer, CancellationToken cancellationToken)
    {
        using JpegDecoderCore jpegDecoder = new(this.options, this.metadata.IccProfile);
        Configuration configuration = this.options.GeneralOptions.Configuration;
        switch (this.photometricInterpretation)
        {
            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
            {
                using SpectralConverter<L8> spectralConverterGray = new GrayJpegSpectralConverter<L8>(configuration);
                HuffmanScanDecoder scanDecoderGray = new(stream, spectralConverterGray, cancellationToken);

                jpegDecoder.LoadTables(this.jpegTables, scanDecoderGray);
                jpegDecoder.ParseStream(stream, spectralConverterGray, cancellationToken);

                _ = this.options.GeneralOptions.TryGetIccProfileForColorConversion(
                    jpegDecoder.Metadata?.IccProfile,
                    out IccProfile? profile);

                using Buffer2D<L8> decompressedBuffer = spectralConverterGray.GetPixelBuffer(profile, cancellationToken);
                JpegCompressionUtils.CopyImageBytesToBuffer(spectralConverterGray.Configuration, buffer, decompressedBuffer);
                break;
            }

            case TiffPhotometricInterpretation.YCbCr:
            case TiffPhotometricInterpretation.Rgb:
            case TiffPhotometricInterpretation.Separated:
            {
                using SpectralConverter<Rgb24> spectralConverter = new TiffJpegSpectralConverter<Rgb24>(configuration, this.photometricInterpretation);
                HuffmanScanDecoder scanDecoder = new(stream, spectralConverter, cancellationToken);

                jpegDecoder.LoadTables(this.jpegTables, scanDecoder);
                jpegDecoder.ParseStream(stream, spectralConverter, cancellationToken);

                _ = this.options.GeneralOptions.TryGetIccProfileForColorConversion(
                    jpegDecoder.Metadata?.IccProfile,
                    out IccProfile? profile);

                using Buffer2D<Rgb24> decompressedBuffer = spectralConverter.GetPixelBuffer(profile, cancellationToken);
                JpegCompressionUtils.CopyImageBytesToBuffer(spectralConverter.Configuration, buffer, decompressedBuffer);
                break;
            }

            default:
                TiffThrowHelper.ThrowNotSupported($"Jpeg compressed tiff with photometric interpretation {this.photometricInterpretation} is not supported");
                break;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
