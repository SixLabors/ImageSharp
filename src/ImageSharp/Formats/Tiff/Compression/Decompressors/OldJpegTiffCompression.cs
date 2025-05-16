// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

internal sealed class OldJpegTiffCompression : TiffBaseDecompressor
{
    private readonly JpegDecoderOptions options;

    private readonly uint startOfImageMarker;

    private readonly TiffPhotometricInterpretation photometricInterpretation;

    public OldJpegTiffCompression(
        JpegDecoderOptions options,
        MemoryAllocator memoryAllocator,
        int width,
        int bitsPerPixel,
        uint startOfImageMarker,
        TiffPhotometricInterpretation photometricInterpretation)
        : base(memoryAllocator, width, bitsPerPixel)
    {
        this.options = options;
        this.startOfImageMarker = startOfImageMarker;
        this.photometricInterpretation = photometricInterpretation;
    }

    protected override void Decompress(BufferedReadStream stream, int byteCount, int stripHeight, Span<byte> buffer, CancellationToken cancellationToken)
    {
        long stripOffset = stream.Position;
        stream.Position = this.startOfImageMarker;

        this.DecodeJpegData(stream, buffer, cancellationToken);

        // Setting the stream position to the expected position.
        // This is a workaround for some images having set the stripBytesCount not equal to the compressed jpeg data.
        stream.Position = stripOffset + byteCount;
    }

    private void DecodeJpegData(BufferedReadStream stream, Span<byte> buffer, CancellationToken cancellationToken)
    {
        using JpegDecoderCore jpegDecoder = new(this.options);
        Configuration configuration = this.options.GeneralOptions.Configuration;
        switch (this.photometricInterpretation)
        {
            case TiffPhotometricInterpretation.BlackIsZero:
            case TiffPhotometricInterpretation.WhiteIsZero:
            {
                using SpectralConverter<L8> spectralConverterGray = new GrayJpegSpectralConverter<L8>(configuration);

                jpegDecoder.ParseStream(stream, spectralConverterGray, cancellationToken);

                _ = this.options.GeneralOptions.TryGetIccProfileForColorConversion(
                    jpegDecoder.Metadata?.IccProfile,
                    out IccProfile? profile);

                using Buffer2D<L8> decompressedBuffer = spectralConverterGray.GetPixelBuffer(
                    profile,
                    cancellationToken);
                JpegCompressionUtils.CopyImageBytesToBuffer(spectralConverterGray.Configuration, buffer, decompressedBuffer);
                break;
            }

            case TiffPhotometricInterpretation.YCbCr:
            case TiffPhotometricInterpretation.Rgb:
            {
                using SpectralConverter<Rgb24> spectralConverter = new TiffOldJpegSpectralConverter<Rgb24>(configuration, this.photometricInterpretation);

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
