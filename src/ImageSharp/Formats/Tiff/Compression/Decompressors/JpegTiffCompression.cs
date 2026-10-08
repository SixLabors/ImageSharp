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
        : base(memoryAllocator, width, bitsPerPixel, TiffPredictor.None)
    {
        this.options = options;
        this.metadata = metadata;
        this.jpegTables = jpegTables;
        this.photometricInterpretation = photometricInterpretation;
    }

    /// <inheritdoc/>
    public override bool DecodesPixels => true;

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
        => this.DecodeJpegData(stream, stripHeight, pixels, cancellationToken);

    /// <summary>
    /// Applies the nested JPEG color conversion and copies its TPixel output into the TIFF destination.
    /// </summary>
    /// <typeparam name="TPixel">The requested image pixel format.</typeparam>
    /// <param name="stream">The nested JPEG stream.</param>
    /// <param name="stripHeight">The encoded strip or tile height.</param>
    /// <param name="buffer">The visible TIFF destination region.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private void DecodeJpegData<TPixel>(BufferedReadStream stream, int stripHeight, Buffer2DRegion<TPixel> buffer, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using JpegDecoderCore jpegDecoder = new(this.options, this.metadata.IccProfile);
        Configuration configuration = this.options.GeneralOptions.Configuration;
        switch (this.photometricInterpretation)
        {
            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
            {
                using SpectralConverter<TPixel> spectralConverterGray = new GrayJpegSpectralConverter<TPixel>(configuration, this.photometricInterpretation);

                if (this.jpegTables is not null)
                {
                    HuffmanScanDecoder scanDecoderGray = new(stream, spectralConverterGray, cancellationToken);
                    jpegDecoder.LoadTables(this.jpegTables, scanDecoderGray);
                }

                jpegDecoder.ParseStream(stream, spectralConverterGray, cancellationToken);

                // The IFD profile takes precedence; otherwise retain the nested APP2 source profile.
                this.metadata.IccProfile ??= jpegDecoder.Metadata?.IccProfile;

                _ = IccProfile.TryGetIccProfileForColorConversion(this.metadata.IccProfile, out IccProfile? profile);

                using Buffer2D<TPixel> decompressedBuffer = spectralConverterGray.GetPixelBuffer(profile, cancellationToken);
                this.CopyPixels(decompressedBuffer, stripHeight, buffer);
                break;
            }

            case TiffPhotometricInterpretation.YCbCr:
            case TiffPhotometricInterpretation.Rgb:
            case TiffPhotometricInterpretation.Separated:
            {
                // Complete JPEG streams retain normal JPEG color deduction; table-based strips use TIFF's photometric interpretation.
                using SpectralConverter<TPixel> spectralConverter = this.jpegTables is null
                    ? new SpectralConverter<TPixel>(configuration)
                    : new TiffJpegSpectralConverter<TPixel>(configuration, this.photometricInterpretation);

                if (this.jpegTables is not null)
                {
                    HuffmanScanDecoder scanDecoder = new(stream, spectralConverter, cancellationToken);
                    jpegDecoder.LoadTables(this.jpegTables, scanDecoder);
                }

                jpegDecoder.ParseStream(stream, spectralConverter, cancellationToken);

                this.metadata.IccProfile ??= jpegDecoder.Metadata?.IccProfile;

                _ = IccProfile.TryGetIccProfileForColorConversion(this.metadata.IccProfile, out IccProfile? profile);

                using Buffer2D<TPixel> decompressedBuffer = spectralConverter.GetPixelBuffer(profile, cancellationToken);
                this.CopyPixels(decompressedBuffer, stripHeight, buffer);
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
