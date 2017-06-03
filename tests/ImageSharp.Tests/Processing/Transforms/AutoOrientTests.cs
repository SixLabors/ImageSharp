// <copyright file="AutoOrientTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;

    using Xunit;

    public class AutoOrientTests : FileTestBase
    {
        public static readonly string[] FlipFiles = { TestImages.Bmp.F };

        public static readonly TheoryData<RotateType, FlipType, ushort> OrientationValues
            = new TheoryData<RotateType, FlipType, ushort>
        {
            { RotateType.None,      FlipType.None,       0 },
            { RotateType.None,      FlipType.None,       1 },
            { RotateType.None,      FlipType.Horizontal, 2 },
            { RotateType.Rotate180, FlipType.None,       3 },
            { RotateType.Rotate180, FlipType.Horizontal, 4 },
            { RotateType.Rotate90,  FlipType.Horizontal, 5 },
            { RotateType.Rotate270, FlipType.None,       6 },
            { RotateType.Rotate90,  FlipType.Vertical,   7 },
            { RotateType.Rotate90,  FlipType.None,       8 },
        };

        [Theory]
        [WithFileCollection(nameof(FlipFiles), nameof(OrientationValues), DefaultPixelType)]
        public void ImageShouldAutoRotate<TPixel>(TestImageProvider<TPixel> provider, RotateType rotateType, FlipType flipType, ushort orientation)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.MetaData.ExifProfile = new ExifProfile();
                image.MetaData.ExifProfile.SetValue(ExifTag.Orientation, orientation);

                image.RotateFlip(rotateType, flipType)
                    .DebugSave(provider, string.Join("_", rotateType, flipType, orientation, "1_before"), Extensions.Bmp)
                    .AutoOrient()
                    .DebugSave(provider, string.Join("_", rotateType, flipType, orientation, "2_after"), Extensions.Bmp);
            }
        }
    }
}