// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

using Moq;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing
{
    /// <summary>
    /// Tests the configuration class.
    /// </summary>
    public class ImageOperationTests : IDisposable
    {
        private readonly Image<Rgba32> image;
        private readonly FakeImageOperationsProvider provider;
        private readonly IImageProcessor processorDefinition;

        private static readonly string ExpectedExceptionMessage = GetExpectedExceptionText();

        public ImageOperationTests()
        {
            this.provider = new FakeImageOperationsProvider();

            var processorMock = new Mock<IImageProcessor>();
            this.processorDefinition = processorMock.Object;

            this.image = new Image<Rgba32>(
                new Configuration
                {
                    ImageOperationsProvider = this.provider
                },
                1,
                1);
        }

        [Fact]
        public void MutateCallsImageOperationsProvider_Func_OriginalImage()
        {
            this.image.Mutate(x => x.ApplyProcessor(this.processorDefinition));

            Assert.True(this.provider.HasCreated(this.image));
            Assert.Contains(
                this.processorDefinition,
                this.provider.AppliedOperations(this.image).Select(x => x.NonGenericProcessor));
        }

        [Fact]
        public void MutateCallsImageOperationsProvider_ListOfProcessors_OriginalImage()
        {
            this.image.Mutate(this.processorDefinition);

            Assert.True(this.provider.HasCreated(this.image));
            Assert.Contains(
                this.processorDefinition,
                this.provider.AppliedOperations(this.image).Select(x => x.NonGenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_Func_WithDuplicateImage()
        {
            Image<Rgba32> returned = this.image.Clone(x => x.ApplyProcessor(this.processorDefinition));

            Assert.True(this.provider.HasCreated(returned));
            Assert.Contains(
                this.processorDefinition,
                this.provider.AppliedOperations(returned).Select(x => x.NonGenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_ListOfProcessors_WithDuplicateImage()
        {
            Image<Rgba32> returned = this.image.Clone(this.processorDefinition);

            Assert.True(this.provider.HasCreated(returned));
            Assert.Contains(
                this.processorDefinition,
                this.provider.AppliedOperations(returned).Select(x => x.NonGenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_Func_NotOnOriginal()
        {
            this.image.Clone(x => x.ApplyProcessor(this.processorDefinition));
            Assert.False(this.provider.HasCreated(this.image));
            Assert.DoesNotContain(
                this.processorDefinition,
                this.provider.AppliedOperations(this.image).Select(x => x.NonGenericProcessor));
        }

        [Fact]
        public void CloneCallsImageOperationsProvider_ListOfProcessors_NotOnOriginal()
        {
            this.image.Clone(this.processorDefinition);
            Assert.False(this.provider.HasCreated(this.image));
            Assert.DoesNotContain(
                this.processorDefinition,
                this.provider.AppliedOperations(this.image).Select(x => x.NonGenericProcessor));
        }

        [Fact]
        public void ApplyProcessors_ListOfProcessors_AppliesAllProcessorsToOperation()
        {
            var operations = new FakeImageOperationsProvider.FakeImageOperations<Rgba32>(Configuration.Default, null, false);
            operations.ApplyProcessors(this.processorDefinition);
            Assert.Contains(this.processorDefinition, operations.Applied.Select(x => x.NonGenericProcessor));
        }

        public void Dispose() => this.image.Dispose();

        [Fact]
        public void GenericMutate_WhenDisposed_Throws()
        {
            this.image.Dispose();

            CheckThrowsCorrectObjectDisposedException(
                () => this.image.Mutate(x => x.ApplyProcessor(this.processorDefinition)));
        }

        [Fact]
        public void GenericClone_WhenDisposed_Throws()
        {
            this.image.Dispose();

            CheckThrowsCorrectObjectDisposedException(
                () => this.image.Clone(x => x.ApplyProcessor(this.processorDefinition)));
        }

        [Fact]
        public void AgnosticMutate_WhenDisposed_Throws()
        {
            this.image.Dispose();
            Image img = this.image;

            CheckThrowsCorrectObjectDisposedException(
                () => img.Mutate(x => x.ApplyProcessor(this.processorDefinition)));
        }

        [Fact]
        public void AgnosticClone_WhenDisposed_Throws()
        {
            this.image.Dispose();
            Image img = this.image;

            CheckThrowsCorrectObjectDisposedException(
                () => img.Clone(x => x.ApplyProcessor(this.processorDefinition)));
        }

        private static string GetExpectedExceptionText()
        {
            try
            {
                var img = new Image<Rgba32>(1, 1);
                img.Dispose();
                img.EnsureNotDisposed();
            }
            catch (ObjectDisposedException ex)
            {
                return ex.Message;
            }

            return "?";
        }

        private static void CheckThrowsCorrectObjectDisposedException(Action action)
        {
            var ex = Assert.Throws<ObjectDisposedException>(action);
            Assert.Equal(ExpectedExceptionMessage, ex.Message);
        }
    }
}
