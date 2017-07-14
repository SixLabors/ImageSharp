using System;
using System.Collections.Generic;
using System.Text;
using ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace ImageSharp.Tests
{
    public abstract class BaseImageOperationsExtensionTest
    {
        protected readonly FakeImageOperationsProvider.FakeImageOperations<Rgba32> operations;
        protected readonly Rectangle rect;
        protected readonly GraphicsOptions options;

        public BaseImageOperationsExtensionTest()
        {
            this.options = new GraphicsOptions(false) { };
            this.rect = new Rectangle(91, 123, 324, 56); // make this random?
            this.operations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(null, false);
        }

        public T Verify<T>(int index = 0)
        {
            Assert.InRange(index, 0, this.operations.applied.Count - 1);

            var operation = this.operations.applied[index];

            return Assert.IsType<T>(operation.Processor);
        }

        public T Verify<T>(Rectangle rect, int index = 0)
        {
            Assert.InRange(index, 0, this.operations.applied.Count - 1);

            var operation = this.operations.applied[index];

            Assert.Equal(rect, operation.Rectangle);
            return Assert.IsType<T>(operation.Processor);
        }
    }
}
