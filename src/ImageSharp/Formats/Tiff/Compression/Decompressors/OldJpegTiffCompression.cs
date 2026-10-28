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
/// Decodes an OldJPEG TIFF stream in the requested pixel format.
/// </summary>
internal sealed class OldJpegTiffCompression : TiffBaseDecompressor
{
    private readonly JpegDecoderOptions options;

    private readonly uint startOfImageMarker;

    private readonly ImageFrameMetadata metadata;

    private readonly TiffPhotometricInterpretation photometricInterpretation;

    /// <summary>
    /// Initializes a new instance of the <see cref="OldJpegTiffCompression"/> class.
    /// </summary>
    /// <param name="options">The JPEG decoder options.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="width">The encoded strip or tile width.</param>
    /// <param name="bitsPerPixel">The total number of bits in each TIFF source pixel.</param>
    /// <param name="metadata">The frame's source profile and format metadata.</param>
    /// <param name="startOfImageMarker">The JPEG stream offset.</param>
    /// <param name="photometricInterpretation">The TIFF source color interpretation.</param>
    public OldJpegTiffCompression(
        JpegDecoderOptions options,
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        ImageFrameMetadata metadata,
        uint startOfImageMarker,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(memoryAllocator, width, bitsPerPixel, TiffPredictor.None)
    {
        this.options = options;
        this.startOfImageMarker = startOfImageMarker;
        this.metadata = metadata;
        this.photometricInterpretation = photometricInterpretation;
    }

    /// <inheritdoc/>
    public override bool DecodesPixels => true;

    /// <inheritdoc/>
    protected override void Decompress<TPixel>(
        BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, Buffer2DRegion<TPixel> pixels, CancellationToken cancellationToken)
    {
        long stripOffset = stream.Position;
        stream.Position = this.startOfImageMarker;

        this.DecodeJpegData(stream, stripHeight, pixels, cancellationToken);

        // Setting the stream position to the expected position.
        // This is a workaround for some images having set the stripBytesCount not equal to the compressed jpeg data.
        stream.Position = stripOffset + byteCount;
    }

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

                jpegDecoder.ParseStream(stream, spectralConverterGray, cancellationToken);

                // The nested APP2 profile remains the TIFF frame's source profile when the IFD has none.
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
                using SpectralConverter<TPixel> spectralConverter = new TiffOldJpegSpectralConverter<TPixel>(configuration, this.photometricInterpretation);

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
