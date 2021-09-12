// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    public abstract class TiffDecoderBaseTester
    {
        protected static TiffDecoder TiffDecoder => new TiffDecoder();

        protected static MagickReferenceDecoder ReferenceDecoder => new MagickReferenceDecoder();

        protected static void TestTiffDecoder<TPixel>(TestImageProvider<TPixel> provider, IImageDecoder referenceDecoder = null, bool useExactComparer = true, float compareTolerance = 0.001f)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage(TiffDecoder);
            image.DebugSave(provider);
            image.CompareToOriginal(
                provider,
                useExactComparer ? ImageComparer.Exact : ImageComparer.Tolerant(compareTolerance),
                referenceDecoder ?? ReferenceDecoder);
        }
    }
}