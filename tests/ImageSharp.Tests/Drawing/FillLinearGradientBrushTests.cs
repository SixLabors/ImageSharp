// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Drawing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class FillLinearGradientBrushTests : FileTestBase
    {
        [Fact]
        public void LinearGradientBrushWithEqualColorsReturnsUnicolorImage()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "LinearGradientBrush");
            using (var image = new Image<Rgba32>(500, 500))
            {
                LinearGradientBrush<Rgba32> unicolorLinearGradientBrush = 
                    new LinearGradientBrush<Rgba32>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(500, 0),
                        new LinearGradientBrush<Rgba32>.ColorStop(0, Rgba32.Red),
                        new LinearGradientBrush<Rgba32>.ColorStop(1, Rgba32.Red));
                
                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.Save($"{path}/UnicolorGradient.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.Red, sourcePixels[0, 0]);
                    Assert.Equal(Rgba32.Red, sourcePixels[9, 9]);
                    Assert.Equal(Rgba32.Red, sourcePixels[199, 149]);
                    Assert.Equal(Rgba32.Red, sourcePixels[499, 499]);
                }
            }
        }
        
        [Fact]
        public void HorizontalLinearGradientBrushReturnsUnicolorColumns()
        {
            int width = 500;
            int height = 500;
            int lastColumnIndex = width - 1;
            int lastRowIndex = height - 1;
            
            
            string path = TestEnvironment.CreateOutputDirectory("Fill", "LinearGradientBrush");
            using (var image = new Image<Rgba32>(width, height))
            {
                LinearGradientBrush<Rgba32> unicolorLinearGradientBrush = 
                    new LinearGradientBrush<Rgba32>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(500, 0),
                        new LinearGradientBrush<Rgba32>.ColorStop(0, Rgba32.Red),
                        new LinearGradientBrush<Rgba32>.ColorStop(1, Rgba32.Yellow));
                
                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.Save($"{path}/horizontalGradient.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Rgba32 columnColor23 = sourcePixels[23, 0];
                    Rgba32 columnColor42 = sourcePixels[42, 0];
                    Rgba32 columnColor333 = sourcePixels[333, 0];
                    
                    for (int i = 0; i < height; i++)
                    {
                        // check first and last column, these are known:
                        Assert.Equal(Rgba32.Red, sourcePixels[0, i]);
                        Assert.Equal(Rgba32.Yellow, sourcePixels[lastColumnIndex, i]);
                        
                        // check the random colors:
                        Assert.Equal(columnColor23, sourcePixels[23, i]);
                        Assert.Equal(columnColor42, sourcePixels[42, i]);
                        Assert.Equal(columnColor333, sourcePixels[333, i]);
                    }
                }
            }
        }
    }
}