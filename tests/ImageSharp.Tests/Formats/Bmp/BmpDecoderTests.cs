// <copyright file="BmpEncoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageSharp.Formats;

namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class BmpDecoderTests : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgb24)]
        public void OpenAllBmpFiles_SaveBmp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }
    }
}