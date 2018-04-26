// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public abstract class BaseImageOperationsExtensionTest
    {
        protected readonly IImageProcessingContext<Rgba32> operations;
        private readonly FakeImageOperationsProvider.FakeImageOperations<Rgba32> internalOperations;
        protected readonly Rectangle rect;
        protected readonly GraphicsOptions options;
        private Image<Rgba32> source;

        public BaseImageOperationsExtensionTest()
        {
            this.options = new GraphicsOptions(false);
            this.source = new Image<Rgba32>(91 + 324, 123 + 56);
            this.rect = new Rectangle(91, 123, 324, 56); // make this random?
            this.internalOperations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(this.source, false);
            this.operations = this.internalOperations;
        }

        public T Verify<T>(int index = 0)
        {
            Assert.InRange(index, 0, this.internalOperations.Applied.Count - 1);

            var operation = this.internalOperations.Applied[index];

            return Assert.IsType<T>(operation.Processor);
        }

        public T Verify<T>(Rectangle rect, int index = 0)
        {
            Assert.InRange(index, 0, this.internalOperations.Applied.Count - 1);

            var operation = this.internalOperations.Applied[index];

            Assert.Equal(rect, operation.Rectangle);
            return Assert.IsType<T>(operation.Processor);
        }
    }
}
