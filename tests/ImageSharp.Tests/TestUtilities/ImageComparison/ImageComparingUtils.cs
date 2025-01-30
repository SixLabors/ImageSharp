// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

public static class ImageComparingUtils
{
    public static void CompareWithReferenceDecoder<TPixel>(
        TestImageProvider<TPixel> provider,
        Image<TPixel> image,
        bool useExactComparer = true,
        float compareTolerance = 0.01f)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        string path = TestImageProvider<TPixel>.GetFilePathOrNull(provider)
            ?? throw new InvalidOperationException("CompareToOriginal() works only with file providers!");

        TestFile testFile = TestFile.Create(path);
        using Image<Rgba32> magickImage = DecodeWithMagick<Rgba32>(new(testFile.FullPath));
        if (useExactComparer)
        {
            ImageComparer.Exact.VerifySimilarity(magickImage, image);
        }
        else
        {
            ImageComparer.Tolerant(compareTolerance).VerifySimilarity(magickImage, image);
        }
    }

    public static Image<TPixel> DecodeWithMagick<TPixel>(FileInfo fileInfo)
        where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
    {
        Configuration configuration = Configuration.Default.Clone();
        configuration.PreferContiguousImageBuffers = true;
        using MagickImage magickImage = new(fileInfo);
        magickImage.AutoOrient();
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
