// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;

using ImageMagick;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff
{
    public static class TiffTestUtils
    {
        public static void CompareWithReferenceDecoder<TPixel>(
            string encodedImagePath,
            Image<TPixel> image,
            bool useExactComparer = true,
            float compareTolerance = 0.01f)
            where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
        {
            var testFile = TestFile.Create(encodedImagePath);
            Image<Rgba32> magickImage = DecodeWithMagick<Rgba32>(Configuration.Default, new FileInfo(testFile.FullPath));
            if (useExactComparer)
            {
                ImageComparer.Exact.VerifySimilarity(magickImage, image);
            }
            else
            {
                ImageComparer.Tolerant(compareTolerance).VerifySimilarity(magickImage, image);
            }
        }

        public static Image<TPixel> DecodeWithMagick<TPixel>(Configuration configuration, FileInfo fileInfo)
            where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
        {
            using var magickImage = new MagickImage(fileInfo);
            magickImage.AutoOrient();
            var result = new Image<TPixel>(configuration, magickImage.Width, magickImage.Height);

            Assert.True(result.TryGetSinglePixelSpan(out Span<TPixel> resultPixels));

            using IUnsafePixelCollection<ushort> pixels = magickImage.GetPixelsUnsafe();
            byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

            PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                configuration,
                data,
                resultPixels,
                resultPixels.Length);

            return result;
        }
    }

    internal class NumberComparer : IEqualityComparer<Number>
    {
        public bool Equals(Number x, Number y) => x.Equals(y);

        public int GetHashCode(Number obj) => obj.GetHashCode();
    }
}
