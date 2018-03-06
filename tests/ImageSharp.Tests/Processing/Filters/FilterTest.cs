// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    using SixLabors.ImageSharp.Processing.Filters;
    using SixLabors.ImageSharp.Processing.Filters.Processors;

    public class FilterTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Filter_CorrectProcessor()
        {
            this.operations.Filter(MatrixFilters.AchromatomalyFilter * MatrixFilters.CreateHueFilter(90F));
            FilterProcessor<Rgba32> p = this.Verify<FilterProcessor<Rgba32>>();
        }

        [Fact]
        public void Filter_rect_CorrectProcessor()
        {
            this.operations.Filter(MatrixFilters.AchromatomalyFilter * MatrixFilters.CreateHueFilter(90F), this.rect);
            FilterProcessor<Rgba32> p = this.Verify<FilterProcessor<Rgba32>>(this.rect);
        }
    }
}