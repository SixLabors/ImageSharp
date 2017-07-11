using System;
using System.Collections.Generic;
using System.Text;
using ImageSharp.Processing;
using Xunit;

namespace ImageSharp.Tests
{
    public abstract class BaseImageOperationsExtensionTest
    {
        protected readonly FakeImageOperationsProvider.FakeImageOperations<Rgba32> operations;

        public BaseImageOperationsExtensionTest()
        {
            this.operations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(null);
        }

        public T Verify<T>(int index = 0)
        {
            var operation = this.operations.applied[index];

            return Assert.IsType<T>(operation.Processor);
        }
    }
}
