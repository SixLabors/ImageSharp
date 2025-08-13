// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable CA1416 // Validate platform compatibility
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SDBitmap = System.Drawing.Bitmap;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

public class SystemDrawingReferenceDecoder : ImageDecoder
{
    private readonly IImageFormat imageFormat;

    public SystemDrawingReferenceDecoder(IImageFormat imageFormat)
        => this.imageFormat = imageFormat;

    public static SystemDrawingReferenceDecoder Png { get; } = new(PngFormat.Instance);

    public static SystemDrawingReferenceDecoder Bmp { get; } = new(BmpFormat.Instance);

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image<Rgba32> image = this.Decode<Rgba32>(options, stream, cancellationToken);
        ImageMetadata metadata = image.Metadata;
        return new ImageInfo(image.Size, metadata, new List<ImageFrameMetadata>(image.Frames.Select(x => x.Metadata)))
        {
            PixelType = metadata.GetDecodedPixelTypeInfo()
        };
    }

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using SDBitmap sourceBitmap = new(stream);
        if (sourceBitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
        {
            return SystemDrawingBridge.From32bppArgbSystemDrawingBitmap<TPixel>(sourceBitmap);
        }

        using SDBitmap convertedBitmap = new(
            sourceBitmap.Width,
            sourceBitmap.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(convertedBitmap))
        {
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            g.DrawImage(sourceBitmap, 0, 0, sourceBitmap.Width, sourceBitmap.Height);
        }

        return ReferenceCodecUtilities.EnsureDecodedMetadata(
            SystemDrawingBridge.From32bppArgbSystemDrawingBitmap<TPixel>(convertedBitmap),
            this.imageFormat);
    }

    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);
}
