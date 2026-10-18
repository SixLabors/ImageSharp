// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Exr;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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

    public static MagickReferenceDecoder Png { get; } = new(PngFormat.Instance);

    public static MagickReferenceDecoder Bmp { get; } = new(BmpFormat.Instance);

    public static MagickReferenceDecoder Jpeg { get; } = new(JpegFormat.Instance);

    public static MagickReferenceDecoder Tiff { get; } = new(TiffFormat.Instance);

    public static MagickReferenceDecoder WebP { get; } = new(WebpFormat.Instance);

    public static MagickReferenceDecoder Exr { get; } = new(ExrFormat.Instance);

    public static MagickReferenceDecoder Heif { get; } = new(HeifFormat.Instance);

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

        JpegReadDefines jpegReadDefines = new()
        {
            // Disable interpolation to match ImageSharp's replicated chroma samples.
            FancyUpsampling = false
        };

        MagickReadSettings settings = new()
        {
            FrameCount = options.MaxFrames
        };

        settings.SetDefines(bmpReadDefines);
        settings.SetDefines(pngReadDefines);
        settings.SetDefines(jpegReadDefines);

        using MagickImageCollection magickImageCollection = new(stream, settings);
        int imageWidth = (int)magickImageCollection.Max(x => x.Width);
        int imageHeight = (int)magickImageCollection.Max(x => x.Height);

        List<ImageFrame<TPixel>> framesList = [];
        foreach (IMagickImage<float> magicFrame in magickImageCollection)
        {
            uint sourceDepth = magicFrame.Depth;

            // In Magick.NET 14.16.0, ImportGrayQuantum ignores min_is_white for 32-bit samples.
            // Apply the missing grayscale inversion through the native API before ICC conversion. Leave alpha unchanged.
            if (this.imageFormat is TiffFormat && sourceDepth == 32 && magicFrame.GetAttribute("tiff:photometric") == "min-is-white")
            {
                magicFrame.Negate(Channels.Gray);
            }

            bool transformed = false;
            ImageMagick.IColorProfile sourceColorProfile = magicFrame.GetColorProfile();
            if (sourceColorProfile is not null)
            {
                IccProfile sourceProfile = new(sourceColorProfile.ToByteArray());
                if (sourceProfile.CheckIsValid() && IccProfile.TryGetIccProfileForColorConversion(sourceProfile, out IccProfile profile))
                {
                    // Magick's intent values differ from ICC values; use the embedded intent without black point compensation.
                    magicFrame.RenderingIntent = profile.Header.RenderingIntent switch
                    {
                        IccRenderingIntent.Perceptual => RenderingIntent.Perceptual,
                        IccRenderingIntent.MediaRelativeColorimetric => RenderingIntent.Relative,
                        IccRenderingIntent.Saturation => RenderingIntent.Saturation,
                        IccRenderingIntent.AbsoluteColorimetric => RenderingIntent.Absolute,
                        _ => RenderingIntent.Undefined
                    };

                    magicFrame.BlackPointCompensation = false;
                    transformed = magicFrame.TransformColorSpace(new ImageMagick.ColorProfile(CompactSrgbV4Profile.Profile.ToByteArray()));
                }
            }

            ImageFrame<TPixel> frame = new(configuration, imageWidth, imageHeight);
            framesList.Add(frame);

            Buffer2DRegion<TPixel> buffer = frame.PixelBuffer.GetRegion(
                (int)(imageWidth - magicFrame.Width),
                (int)(imageHeight - magicFrame.Height),
                (int)magicFrame.Width,
                (int)magicFrame.Height);

            using IUnsafePixelCollection<float> pixels = magicFrame.GetPixelsUnsafe();

            // HEIF needs the ushort export below to recover its left-aligned ten- and twelve-bit samples.
            // Other formats retain this branch's existing byte-reference path at those depths.
            bool isHighDepthHeif = this.imageFormat is HeifFormat && sourceDepth is 10 or 12;
            bool isLowDepth = !isHighDepthHeif && sourceDepth is 12 or 10 or 8 or 6 or 5 or 4 or 3 or 2 or 1;
            bool isHighDepth = isHighDepthHeif || sourceDepth is 14 or 16 or 32;

            // ICC conversion changes the samples, not the PNG's declared bit depth.
            if (isHighDepth && this.imageFormat is PngFormat)
            {
                metadata.GetPngMetadata().BitDepth = PngBitDepth.Bit16;
            }

            if (transformed)
            {
                // Magick's Q16-HDRI samples use 65535 for full intensity and opaque alpha, but ICC output can exceed that range.
                // Read the float samples directly. Exporting bytes or ushorts would clip RGB and hide differences from our decoder.
                // Dividing by Quantum.Max maps full intensity to 1 while retaining negative RGB and RGB above 1.
                float[] data = pixels.GetValues();
                TensorPrimitives.Multiply(data, 1F / Quantum.Max, data);
                ReadOnlySpan<float> samples = data;
                int rowLength = buffer.Width * (int)magicFrame.ChannelCount;
                using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(buffer.Width);

                for (int y = 0; y < buffer.Height; y++)
                {
                    Span<Vector4> row = vectors.Memory.Span;
                    if (magicFrame.HasAlpha)
                    {
                        MemoryMarshal.Cast<float, Vector4>(samples[..rowLength]).CopyTo(row);
                    }
                    else
                    {
                        Rgb.ToScaledVector4(MemoryMarshal.Cast<float, Rgb>(samples[..rowLength]), row);
                    }

                    // Magick supplies straight RGB. This packer applies TPixel's alpha representation, numeric range and precision.
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, row, buffer.DangerousGetRowSpan(y));
                    samples = samples[rowLength..];
                }
            }
            else if (isLowDepth)
            {
                byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                FromRgba32Bytes(configuration, data, buffer);
            }
            else if (isHighDepth)
            {
                ushort[] data = pixels.ToShortArray(PixelMapping.RGBA);
                if (this.imageFormat is HeifFormat && magicFrame.Depth is 10 or 12)
                {
                    // The HEIF reader left-aligns native samples in its 16-bit channels. Recover the declared
                    // sample values before expanding their full range to Rgba64; a shift alone leaves white
                    // below 65535 and changes every nonzero component's normalized value.
                    int depth = (int)magicFrame.Depth;
                    int shift = 16 - depth;
                    int maximum = (1 << depth) - 1;
                    for (int i = 0; i < data.Length; i++)
                    {
                        int sample = data[i] >> shift;
                        data[i] = (ushort)(((sample * 65535) + (maximum / 2)) / maximum);
                    }
                }

                Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data.AsSpan());
                FromRgba64Bytes(configuration, bytes, buffer);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        return ReferenceCodecUtilities.EnsureDecodedMetadata<TPixel>(new Image<TPixel>(configuration, metadata, framesList), this.imageFormat);
    }

    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image<Rgba32> image = this.Decode<Rgba32>(options, stream, cancellationToken);
        ImageMetadata metadata = image.Metadata;
        return new ImageInfo(image.Size, metadata, new List<ImageFrameMetadata>(image.Frames.Select(x => x.Metadata)))
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
