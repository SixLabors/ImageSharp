// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class SolidFillBlendedShapesTests
    {
        public static IEnumerable<object[]> modes =
            ((PixelColorBlendingMode[])Enum.GetValues(typeof(PixelColorBlendingMode))).Select(x => new object[] { x });

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void _1DarkBlueRect_2BlendHotPinkRect<TPixel>(
            TestImageProvider<TPixel> provider,
            PixelColorBlendingMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                int scaleX = img.Width / 100;
                int scaleY = img.Height / 100;
                img.Mutate(
                    x => x.Fill(
                            NamedColors<TPixel>.DarkBlue,
                            new Rectangle(0 * scaleX, 40 * scaleY, 100 * scaleX, 20 * scaleY)
                            )
                        .Fill(new GraphicsOptions(true) { ColorBlendingMode = mode },
                            NamedColors<TPixel>.HotPink,
                            new Rectangle(20 * scaleX, 0 * scaleY, 30 * scaleX, 100 * scaleY))
                    );

                VerifyImage(provider, mode, img);
            }
        }

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void _1DarkBlueRect_2BlendHotPinkRect_3BlendTransparentEllipse<TPixel>(
            TestImageProvider<TPixel> provider,
            PixelColorBlendingMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                int scaleX = img.Width / 100;
                int scaleY = img.Height / 100;
                img.Mutate(
                    x => x.Fill(
                        NamedColors<TPixel>.DarkBlue,
                        new Rectangle(0 * scaleX, 40 * scaleY, 100 * scaleX, 20 * scaleY)));
                img.Mutate(
                    x => x.Fill(
                        new GraphicsOptions(true) { ColorBlendingMode = mode },
                        NamedColors<TPixel>.HotPink,
                        new Rectangle(20 * scaleX, 0 * scaleY, 30 * scaleX, 100 * scaleY)));
                img.Mutate(
                    x => x.Fill(
                        new GraphicsOptions(true) { ColorBlendingMode = mode },
                        NamedColors<TPixel>.Transparent,
                        new Shapes.EllipsePolygon(40 * scaleX, 50 * scaleY, 50 * scaleX, 50 * scaleY))
                    );

                VerifyImage(provider, mode, img);
            }
        }

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void _1DarkBlueRect_2BlendHotPinkRect_3BlendSemiTransparentRedEllipse<TPixel>(
            TestImageProvider<TPixel> provider,
            PixelColorBlendingMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                int scaleX = (img.Width / 100);
                int scaleY = (img.Height / 100);
                img.Mutate(
                    x => x.Fill(
                        NamedColors<TPixel>.DarkBlue,
                        new Rectangle(0 * scaleX, 40, 100 * scaleX, 20 * scaleY)));
                img.Mutate(
                    x => x.Fill(
                        new GraphicsOptions(true) { ColorBlendingMode = mode },
                        NamedColors<TPixel>.HotPink,
                        new Rectangle(20 * scaleX, 0, 30 * scaleX, 100 * scaleY)));
                var c = NamedColors<TPixel>.Red.ToVector4();
                c.W *= 0.5f;
                var pixel = default(TPixel);
                pixel.PackFromVector4(c);

                img.Mutate(
                    x => x.Fill(
                        new GraphicsOptions(true) { ColorBlendingMode = mode },
                        pixel,
                        new Shapes.EllipsePolygon(40 * scaleX, 50 * scaleY, 50 * scaleX, 50 * scaleY))
                    );

                VerifyImage(provider, mode, img); ;
            }
        }

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void _1DarkBlueRect_2BlendBlackEllipse<TPixel>(TestImageProvider<TPixel> provider, PixelColorBlendingMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using(Image<TPixel> dstImg = provider.GetImage(), srcImg = provider.GetImage())
            {
                int scaleX = (dstImg.Width / 100);
                int scaleY = (dstImg.Height / 100);

                dstImg.Mutate(
                    x => x.Fill(
                        NamedColors<TPixel>.DarkBlue,
                        new Rectangle(0 * scaleX, 40 * scaleY, 100 * scaleX, 20 * scaleY)));

                srcImg.Mutate(
                    x => x.Fill(
                        NamedColors<TPixel>.Black,
                        new Shapes.EllipsePolygon(40 * scaleX, 50 * scaleY, 50 * scaleX, 50 * scaleY)));

                dstImg.Mutate(
                    x => x.DrawImage(new GraphicsOptions(true) { ColorBlendingMode = mode }, srcImg)
                    );                

                VerifyImage(provider, mode, dstImg);
            }
        }
        
        private static void VerifyImage<TPixel>(TestImageProvider<TPixel> provider, PixelColorBlendingMode mode, Image<TPixel> img)
            where TPixel : struct, IPixel<TPixel>
        {
            img.DebugSave(
                provider,
                new { mode },
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);

            var comparer = ImageComparer.TolerantPercentage(0.01f, 3);
            img.CompareFirstFrameToReferenceOutput(comparer, 
                provider,
                new { mode },
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);
        }
    }
}