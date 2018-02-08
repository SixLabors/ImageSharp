// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class BlendedShapes
    {
        public static IEnumerable<object[]> modes = ((PixelBlenderMode[])Enum.GetValues(typeof(PixelBlenderMode)))
                                                                    .Select(x=> new object[] { x });

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void DrawBlendedValues<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var img = provider.GetImage())
            {
                var scaleX = (img.Width / 100);
                var scaleY = (img.Height / 100);
                img.Mutate(x=>x
                            .Fill(NamedColors<TPixel>.DarkBlue, new Rectangle(0 * scaleX, 40 * scaleY, 100 * scaleX, 20 * scaleY))
                            .Fill(NamedColors<TPixel>.HotPink, new Rectangle(20 * scaleX, 0 * scaleY, 30 * scaleX, 100 * scaleY), new ImageSharp.GraphicsOptions(true)
                            {
                                BlenderMode = mode
                            }));
                img.DebugSave(provider, new { mode });
            }
        }

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void DrawBlendedValues_transparent<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var img = provider.GetImage())
            {
                var scaleX = (img.Width / 100);
                var scaleY = (img.Height / 100);
                img.Mutate(x=>x.Fill(NamedColors<TPixel>.DarkBlue, new Rectangle(0* scaleX, 40 * scaleY, 100 * scaleX, 20 * scaleY)));
                img.Mutate(x => x.Fill(NamedColors<TPixel>.HotPink, new Rectangle(20 * scaleX, 0 * scaleY, 30 * scaleX, 100 * scaleY), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                }));
                img.Mutate(x => x.Fill(NamedColors<TPixel>.Transparent, new SixLabors.Shapes.EllipsePolygon(40 * scaleX, 50 * scaleY, 50 * scaleX, 50 * scaleY), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                }));
                img.DebugSave(provider, new { mode });
            }
        }

        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void DrawBlendedValues_transparent50Percent<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var img = provider.GetImage())
            {
                var scaleX = (img.Width / 100);
                var scaleY = (img.Height / 100);
                img.Mutate(x=>x.Fill(NamedColors<TPixel>.DarkBlue, new Rectangle(0 * scaleX, 40, 100 * scaleX, 20* scaleY)));
                img.Mutate(x => x.Fill(NamedColors<TPixel>.HotPink, new Rectangle(20 * scaleX, 0, 30 * scaleX, 100 * scaleY), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                }));
                var c = NamedColors<TPixel>.Red.ToVector4();
                c.W *= 0.5f;
                TPixel pixel = default(TPixel);
                pixel.PackFromVector4(c);

                img.Mutate(x => x.Fill(pixel, new SixLabors.Shapes.EllipsePolygon(40 * scaleX, 50 * scaleY, 50 * scaleX, 50 * scaleY), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                }));
                img.DebugSave(provider, new { mode });
            }
        }



        [Theory]
        [WithBlankImages(nameof(modes), 250, 250, PixelTypes.Rgba32)]
        public void DrawBlendedValues_doldidEllips<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var img = provider.GetImage())
            {
                var scaleX = (img.Width / 100);
                var scaleY = (img.Height / 100);
                img.Mutate(x => x.Fill(NamedColors<TPixel>.DarkBlue, new Rectangle(0 * scaleX, 40* scaleY, 100 * scaleX, 20 * scaleY)));
                img.Mutate(x => x.Fill(NamedColors<TPixel>.Black, new SixLabors.Shapes.EllipsePolygon(40 * scaleX, 50 * scaleY, 50 * scaleX, 50 * scaleY), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                }));
                img.DebugSave(provider, new { mode });
            }
        }
    }
}
