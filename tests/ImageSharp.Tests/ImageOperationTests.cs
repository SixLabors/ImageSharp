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
        private readonly IImageProcessor<Rgba32> processorImplementation;

        private readonly IImageProcessor processorDefinition;

        public Configuration Configuration { get; private set; }

        public ImageOperationTests()
        {
            this.provider = new FakeImageOperationsProvider();
            this.processorImplementation = new Mock<IImageProcessor<Rgba32>>().Object;
            
            Mock<IImageProcessor> processorMock = new Mock<IImageProcessor>();
            processorMock.Setup(p => p.CreatePixelSpecificProcessor<Rgba32>()).Returns(this.processorImplementation);
            this.processorDefinition = processorMock.Object;
            
            this.image = new Image<Rgba32>(new Configuration()
            {
                ImageOperationsProvider = this.provider
            }, 1, 1);
        }

        [Fact]
        public void MutateCallsImageOperationsProvider_Func_OriginalImage()
        {
            this.image.Mutate(x => x.ApplyProcessor(this.processorDefinition));

            Assert.True(this.provider.HasCreated(this.image));
            Assert.Contains(this.processorImplementation, this.provider.AppliedOperations(this.image).Select(x => x.GenericProcessor));
        }

        [Fact]
        public void MutateCallsImageOperationsProvider_ListOfProcessors_OriginalImage()
        {
            this.image.Mutate(this.processorDefinition);

            Assert.True(this.provider.HasCreated(this.image));
            Assert.Contains(this.processorImplementation, this.provider.AppliedOperations(this.image).Select(x => x.GenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_Func_WithDuplicateImage()
        {
            Image<Rgba32> returned = this.image.Clone(x => x.ApplyProcessor(this.processorDefinition));

            Assert.True(this.provider.HasCreated(returned));
            Assert.Contains(this.processorImplementation, this.provider.AppliedOperations(returned).Select(x => x.GenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_ListOfProcessors_WithDuplicateImage()
        {
            Image<Rgba32> returned = this.image.Clone(this.processorDefinition);

            Assert.True(this.provider.HasCreated(returned));
            Assert.Contains(this.processorImplementation, this.provider.AppliedOperations(returned).Select(x => x.GenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_Func_NotOnOrigional()
        {
            Image<Rgba32> returned = this.image.Clone(x => x.ApplyProcessor(this.processorDefinition));
            Assert.False(this.provider.HasCreated(this.image));
            Assert.DoesNotContain(this.processorImplementation, this.provider.AppliedOperations(this.image).Select(x => x.GenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_ListOfProcessors_NotOnOrigional()
        {
            Image<Rgba32> returned = this.image.Clone(this.processorDefinition);
            Assert.False(this.provider.HasCreated(this.image));
            Assert.DoesNotContain(this.processorImplementation, this.provider.AppliedOperations(this.image).Select(x => x.GenericProcessor));
        }

        [Fact]
        public void ApplyProcessors_ListOfProcessors_AppliesAllProcessorsToOperation()
        {
            var operations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(null, false);
            operations.ApplyProcessors(this.processorDefinition);
            Assert.Contains(this.processorImplementation, operations.Applied.Select(x => x.GenericProcessor));
        }

        public void Dispose() => this.image.Dispose();
    }
}