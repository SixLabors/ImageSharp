// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable CA1416 // Validate platform compatibility
using System.Drawing.Imaging;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

public class SystemDrawingReferenceEncoder : IImageEncoder
{
    private readonly ImageFormat imageFormat;

    public SystemDrawingReferenceEncoder(ImageFormat imageFormat)
        => this.imageFormat = imageFormat;

    public static SystemDrawingReferenceEncoder Png { get; } = new(ImageFormat.Png);

    public static SystemDrawingReferenceEncoder Bmp { get; } = new(ImageFormat.Bmp);

    public bool SkipMetadata { get; init; }

    public void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.To32bppArgbSystemDrawingBitmap(image);
        sdBitmap.Save(stream, this.imageFormat);
    }

    public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.To32bppArgbSystemDrawingBitmap(image))
        {
            sdBitmap.Save(stream, this.imageFormat);
        }

        return Task.CompletedTask;
    }
}
