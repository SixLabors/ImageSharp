// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing
{
    public class ImageProcessorTest
    {
        [Fact]
        public void Apply_ShouldUseCustomConfiguration()
        {
            Configuration expected = Configuration.Default;
            var source = new Image<Rgba32>(91 + 324, 123 + 56);
            var processor = new FakeProcessor<Rgba32>();

            processor.Apply(source, source.Bounds(), expected);

            Assert.Equal(expected, processor.AppliedConfiguration);
        }
    }

    public class FakeProcessor<TPixel> : IImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        public void Apply(Image<TPixel> source, Rectangle sourceRectangle, Configuration configuration = null)
        {
            this.AppliedConfiguration = configuration;
        }

        public Configuration AppliedConfiguration { get; private set; }
    }
}