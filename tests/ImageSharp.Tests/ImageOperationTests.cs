// <copyright file="ConfigurationTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ImageSharp.Formats;
    using ImageSharp.IO;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using Moq;
    using SixLabors.Primitives;
    using Xunit;

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
            Assert.Contains(this.processor, this.provider.AppliedOperations(this.image).Select(x=>x.Processor));
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
        public void ApplyProcessors_ListOfProcessors_AppliesALlProcessorsToOperation()
        {
            var operations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(null);
            operations.ApplyProcessors(this.processor);
            Assert.Contains(this.processor, operations.applied.Select(x => x.Processor));
        }

        public void Dispose()
        {
            this.image.Dispose();
        }
    }
}