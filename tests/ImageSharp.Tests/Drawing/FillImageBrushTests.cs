// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;
using SixLabors.Shapes;

using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class FillImageBrushTests
    {
        [Fact]
        public void DoesNotDisposeImage()
        {
            using (var src = new Image<Rgba32>(5, 5))
            {
                var brush = new ImageBrush<Rgba32>(src);
                using (var dest = new Image<Rgba32>(10, 10))
                {
                    dest.Mutate(c => c.Fill(brush, new Rectangle(0, 0, 10, 10)));
                    dest.Mutate(c => c.Fill(brush, new Rectangle(0, 0, 10, 10)));
                }
            }
        }
    }
}