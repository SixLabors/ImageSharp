// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

using Moq;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the configuration class.
    /// </summary>
    public class ImageOperationTests : IDisposable
    {
        private readonly Image<Rgba32> image;
        private readonly FakeImageOperationsProvider provider;
        private readonly IImageProcessor<Rgba32> processor;

        public Configuration Configuration { get; private set; }

        public ImageOperationTests()
        {
            this.provider = new FakeImageOperationsProvider();
            this.processor = new Mock<IImageProcessor<Rgba32>>().Object;
            this.image = new Image<Rgba32>(new Configuration()
            {
                ImageOperationsProvider = this.provider
            }, 1, 1);
        }

        [Fact]
        public void MutateCallsImageOperationsProvider_Func_OriginalImage()
        {
            this.image.Mutate(x => x.ApplyProcessor(this.processor));

            Assert.True(this.provider.HasCreated(this.image));
            Assert.Contains(this.processor, this.provider.AppliedOperations(this.image).Select(x => x.Processor));
        }

        [Fact]
        public void MutateCallsImageOperationsProvider_ListOfProcessors_OriginalImage()
        {
            this.image.Mutate(this.processor);

            Assert.True(this.provider.HasCreated(this.image));
            Assert.Contains(this.processor, this.provider.AppliedOperations(this.image).Select(x => x.Processor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_Func_WithDuplicateImage()
        {
            var returned = this.image.Clone(x => x.ApplyProcessor(this.processor));

            Assert.True(this.provider.HasCreated(returned));
            Assert.Contains(this.processor, this.provider.AppliedOperations(returned).Select(x => x.Processor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_ListOfProcessors_WithDuplicateImage()
        {
            var returned = this.image.Clone(this.processor);

            Assert.True(this.provider.HasCreated(returned));
            Assert.Contains(this.processor, this.provider.AppliedOperations(returned).Select(x => x.Processor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_Func_NotOnOrigional()
        {
            var returned = this.image.Clone(x => x.ApplyProcessor(this.processor));
            Assert.False(this.provider.HasCreated(this.image));
            Assert.DoesNotContain(this.processor, this.provider.AppliedOperations(this.image).Select(x => x.Processor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_ListOfProcessors_NotOnOrigional()
        {
            var returned = this.image.Clone(this.processor);
            Assert.False(this.provider.HasCreated(this.image));
            Assert.DoesNotContain(this.processor, this.provider.AppliedOperations(this.image).Select(x => x.Processor));
        }

        [Fact]
        public void ApplyProcessors_ListOfProcessors_AppliesAllProcessorsToOperation()
        {
            var operations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(null, false);
            operations.ApplyProcessors(this.processor);
            Assert.Contains(this.processor, operations.Applied.Select(x => x.Processor));
        }

        public void Dispose()
        {
            this.image.Dispose();
        }
    }
}