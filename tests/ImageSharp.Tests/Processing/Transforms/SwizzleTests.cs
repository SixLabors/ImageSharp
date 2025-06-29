// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms;

[Trait("Category", "Processors")]
public class SwizzleTests : BaseImageOperationsExtensionTest
{
    private struct InvertXAndYSwizzler : ISwizzler
    {
        public InvertXAndYSwizzler(Size sourceSize)
        {
            this.DestinationSize = new Size(sourceSize.Height, sourceSize.Width);
        }

        public Size DestinationSize { get; }

        public Point Transform(Point point) => new(point.Y, point.X);
    }

    [Fact]
    public void InvertXAndYSwizzlerSetsCorrectSizes()
    {
        int width = 5;
        int height = 10;

        this.operations.Swizzle(new InvertXAndYSwizzler(new Size(width, height)));
        SwizzleProcessor<InvertXAndYSwizzler> processor = this.Verify<SwizzleProcessor<InvertXAndYSwizzler>>();

        Assert.Equal(processor.Swizzler.DestinationSize.Width, height);
        Assert.Equal(processor.Swizzler.DestinationSize.Height, width);

        this.operations.Swizzle(new InvertXAndYSwizzler(processor.Swizzler.DestinationSize));
        SwizzleProcessor<InvertXAndYSwizzler> processor2 = this.Verify<SwizzleProcessor<InvertXAndYSwizzler>>(1);

        Assert.Equal(processor2.Swizzler.DestinationSize.Width, width);
        Assert.Equal(processor2.Swizzler.DestinationSize.Height, height);
    }
}
