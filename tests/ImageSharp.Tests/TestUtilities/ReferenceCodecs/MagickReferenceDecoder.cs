// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

public class MagickReferenceDecoder : ImageDecoder
{
    private readonly IImageFormat imageFormat;
    private readonly bool validate;

    public MagickReferenceDecoder(IImageFormat imageFormat)
        : this(imageFormat, true)
    {
    }

    public MagickReferenceDecoder(IImageFormat imageFormat, bool validate)
    {
        this.imageFormat = imageFormat;
        this.validate = validate;
    }

    public static MagickReferenceDecoder Png { get; } = new MagickReferenceDecoder(PngFormat.Instance);

    public static MagickReferenceDecoder Bmp { get; } = new MagickReferenceDecoder(BmpFormat.Instance);

    public static MagickReferenceDecoder Jpeg { get; } = new MagickReferenceDecoder(JpegFormat.Instance);

    public static MagickReferenceDecoder Tiff { get; } = new MagickReferenceDecoder(TiffFormat.Instance);

    public static MagickReferenceDecoder WebP { get; } = new MagickReferenceDecoder(WebpFormat.Instance);

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        ImageMetadata metadata = new();

        Configuration configuration = options.Configuration;
        BmpReadDefines bmpReadDefines = new()
        {
            IgnoreFileSize = !this.validate
        };
        PngReadDefines pngReadDefines = new()
        {
            IgnoreCrc = !this.validate
        };

        MagickReadSettings settings = new()
        {
            FrameCount = (int)options.MaxFrames
        };
        settings.SetDefines(bmpReadDefines);
        settings.SetDefines(pngReadDefines);

        using MagickImageCollection magickImageCollection = new(stream, settings);
        int imageWidth = magickImageCollection.Max(x => x.Width);
        int imageHeight = magickImageCollection.Max(x => x.Height);

        List<ImageFrame<TPixel>> framesList = [];
        foreach (IMagickImage<ushort> magicFrame in magickImageCollection)
        {
            ImageFrame<TPixel> frame = new(configuration, imageWidth, imageHeight);
            framesList.Add(frame);

            Buffer2DRegion<TPixel> buffer = frame.PixelBuffer.GetRegion(
                imageWidth - magicFrame.Width,
                imageHeight - magicFrame.Height,
                magicFrame.Width,
                magicFrame.Height);

            using IUnsafePixelCollection<ushort> pixels = magicFrame.GetPixelsUnsafe();
            if (magicFrame.Depth is 12 or 10 or 8 or 6 or 5 or 4 or 3 or 2 or 1)
            {
                byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                FromRgba32Bytes(configuration, data, buffer);
            }
            else if (magicFrame.Depth is 14 or 16 or 32)
            {
                if (this.imageFormat is PngFormat png)
                {
                    metadata.GetPngMetadata().BitDepth = PngBitDepth.Bit16;
                }

                ushort[] data = pixels.ToShortArray(PixelMapping.RGBA);
                Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data.AsSpan());
                FromRgba64Bytes(configuration, bytes, buffer);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        return ReferenceCodecUtilities.EnsureDecodedMetadata<TPixel>(new(configuration, metadata, framesList), this.imageFormat);
    }

    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image<Rgba32> image = this.Decode<Rgba32>(options, stream, cancellationToken);
        ImageMetadata metadata = image.Metadata;
        return new(image.Size, metadata, new List<ImageFrameMetadata>(image.Frames.Select(x => x.Metadata)))
        {
            PixelType = metadata.GetDecodedPixelTypeInfo()
        };
    }
    private static void FromRgba32Bytes<TPixel>(
        Configuration configuration,
        Span<byte> rgbaBytes,
        Buffer2DRegion<TPixel> destinationGroup)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        Span<Rgba32> sourcePixels = MemoryMarshal.Cast<byte, Rgba32>(rgbaBytes);
        for (int y = 0; y < destinationGroup.Height; y++)
        {
            Span<TPixel> destBuffer = destinationGroup.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgba32(
                configuration,
                sourcePixels[..destBuffer.Length],
                destBuffer);

            sourcePixels = sourcePixels[destBuffer.Length..];
        }
    }

    private static void FromRgba64Bytes<TPixel>(
        Configuration configuration,
        Span<byte> rgbaBytes,
        Buffer2DRegion<TPixel> destinationGroup)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        for (int y = 0; y < destinationGroup.Height; y++)
        {
            Span<TPixel> destBuffer = destinationGroup.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromRgba64Bytes(
                configuration,
                rgbaBytes,
                destBuffer,
                destBuffer.Length);

            rgbaBytes = rgbaBytes[(destBuffer.Length * 8)..];
        }
    }
}
