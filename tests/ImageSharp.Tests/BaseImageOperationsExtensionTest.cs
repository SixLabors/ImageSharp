// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;
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

        public BaseImageOperationsExtensionTest()
        {
            this.options = new GraphicsOptions(false) { };
            this.rect = new Rectangle(91, 123, 324, 56); // make this random?
            this.internalOperations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(null, false);
            this.operations = this.internalOperations;
        }

        public T Verify<T>(int index = 0)
        {
            Assert.InRange(index, 0, this.internalOperations.applied.Count - 1);

            var operation = this.internalOperations.applied[index];

            return Assert.IsType<T>(operation.Processor);
        }

        public T Verify<T>(Rectangle rect, int index = 0)
        {
            Assert.InRange(index, 0, this.internalOperations.applied.Count - 1);

            var operation = this.internalOperations.applied[index];

            Assert.Equal(rect, operation.Rectangle);
            return Assert.IsType<T>(operation.Processor);
        }
    }
}
