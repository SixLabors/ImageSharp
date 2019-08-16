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
        protected readonly IImageProcessingContext operations;
        private readonly FakeImageOperationsProvider.FakeImageOperations<Rgba32> internalOperations;
        protected readonly Rectangle rect;
        protected readonly GraphicsOptions options;
        private readonly Image<Rgba32> source;

        public Rectangle SourceBounds() => this.source.Bounds();

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

            FakeImageOperationsProvider.FakeImageOperations<Rgba32>.AppliedOperation operation = this.internalOperations.Applied[index];

            if (operation.NonGenericProcessor != null)
            {
                return Assert.IsType<T>(operation.NonGenericProcessor);
            }
            
            return Assert.IsType<T>(operation.GenericProcessor);
        }

        public T Verify<T>(Rectangle rect, int index = 0)
        {
            Assert.InRange(index, 0, this.internalOperations.Applied.Count - 1);

            FakeImageOperationsProvider.FakeImageOperations<Rgba32>.AppliedOperation operation = this.internalOperations.Applied[index];

            Assert.Equal(rect, operation.Rectangle);
            
            if (operation.NonGenericProcessor != null)
            {
                return Assert.IsType<T>(operation.NonGenericProcessor);
            }
            
            return Assert.IsType<T>(operation.GenericProcessor);
        }
    }
}
