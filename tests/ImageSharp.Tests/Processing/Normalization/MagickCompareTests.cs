// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization;

// ReSharper disable InconsistentNaming
[Trait("Category", "Processors")]
public class MagickCompareTests
{
    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.ForestBridgeDifferentComponentsQuality, PixelTypes.Rgba32)]
    public void AutoLevel_CompareToMagick<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        Image<TPixel> imageFromMagick;
        using (Stream stream = LoadAsStream(provider))
        {
            using MagickImage magickImage = new(stream);

            // Apply Auto Level using the Grey (BT.709) channel.
            magickImage.AutoLevel(Channels.Gray);
            imageFromMagick = ConvertImageFromMagick<TPixel>(magickImage);
        }

        using Image<TPixel> image = provider.GetImage();
        HistogramEqualizationOptions options = new()
        {
            Method = HistogramEqualizationMethod.AutoLevel,
            LuminanceLevels = 256,
            SyncChannels = true
        };
        image.Mutate(x => x.HistogramEqualization(options));
        image.DebugSave(provider);
        ExactImageComparer.Instance.CompareImages(imageFromMagick, image);

        imageFromMagick.Dispose();
    }

    private static FileStream LoadAsStream<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        string path = TestImageProvider<TPixel>.GetFilePathOrNull(provider)
            ?? throw new InvalidOperationException("CompareToMagick() works only with file providers!");

        TestFile testFile = TestFile.Create(path);
        return new(testFile.FullPath, FileMode.Open);
    }

    private static Image<TPixel> ConvertImageFromMagick<TPixel>(MagickImage magickImage)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        Configuration configuration = Configuration.Default.Clone();
        configuration.PreferContiguousImageBuffers = true;
        Image<TPixel> result = new(configuration, (int)magickImage.Width, (int)magickImage.Height);

        Assert.True(result.DangerousTryGetSinglePixelMemory(out Memory<TPixel> resultPixels));

        using (IUnsafePixelCollection<ushort> pixels = magickImage.GetPixelsUnsafe())
        {
            byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

            PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                configuration,
                data,
                resultPixels.Span,
                resultPixels.Length);
        }

        return result;
    }
}
