// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class BlackWhiteTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void BlackWhite_CorrectProcessor()
        {
            this.operations.BlackWhite();
            this.Verify<BlackWhiteProcessor>();
        }

        [Fact]
        public void BlackWhite_rect_CorrectProcessor()
        {
            this.operations.BlackWhite(this.rect);
            this.Verify<BlackWhiteProcessor>(this.rect);
        }
    }
}
