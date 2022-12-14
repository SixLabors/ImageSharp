// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff;

public abstract class TiffDecoderBaseTester
{
    protected static MagickReferenceDecoder ReferenceDecoder => new();

    protected static void TestTiffDecoder<TPixel>(TestImageProvider<TPixel> provider, IImageDecoder referenceDecoder = null, bool useExactComparer = true, float compareTolerance = 0.001f)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(TiffDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(
            provider,
            useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance),
            referenceDecoder ?? ReferenceDecoder);
    }
}
