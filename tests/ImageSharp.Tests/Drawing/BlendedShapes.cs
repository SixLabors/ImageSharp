// <copyright file="DrawImageEffectTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Text;
    using ImageSharp.PixelFormats;
    using Xunit;

    public class BlendedShapes
    {
        public static IEnumerable<object[]> modes = ((PixelBlenderMode[])Enum.GetValues(typeof(PixelBlenderMode)))
                                                                    .Select(x=> new object[] { x });

        [Theory]
        [WithBlankImages(nameof(modes), 100, 100, PixelTypes.StandardImageClass)]
        public void DrawBlendedValues<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var img = provider.GetImage())
            {
                img.Fill(NamedColors<TPixel>.DarkBlue, new Rectangle(0, 40, 100, 20));
                img.Fill(NamedColors<TPixel>.HotPink, new Rectangle(40, 0, 20, 100), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                });
                img.DebugSave(provider, new { mode });
            }
        }

        [Theory]
        [WithBlankImages(nameof(modes), 100, 100, PixelTypes.StandardImageClass)]
        public void DrawBlendedValues_transparent<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var img = provider.GetImage())
            {
                img.Fill(NamedColors<TPixel>.DarkBlue, new Rectangle(0, 40, 100, 20));
                img.Fill(NamedColors<TPixel>.HotPink, new Rectangle(20, 0, 40, 100), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                });
                img.Fill(NamedColors<TPixel>.Transparent, new Rectangle(40, 0, 20, 100), new ImageSharp.GraphicsOptions(true)
                {
                    BlenderMode = mode
                });
                img.DebugSave(provider, new { mode });
            }
        }
    }
}
