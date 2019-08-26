// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Moq;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing
{
    /// <summary>
    /// Contains test cases for default <see cref="IImageProcessingContext"/> implementation.
    /// </summary>
    public class ImageProcessingContextTests
    {
        private readonly Image image = new Image<Rgba32>(10, 10);

        private readonly Mock<IImageProcessor> processorDefinition;

        private readonly Mock<IImageProcessor<Rgba32>> regularProcessorImpl;

        private readonly Mock<ICloningImageProcessor<Rgba32>> cloningProcessorImpl;

        private static readonly Rectangle Bounds = new Rectangle(3, 3, 5, 5);

        public ImageProcessingContextTests()
        {
            this.processorDefinition = new Mock<IImageProcessor>();
            this.regularProcessorImpl = new Mock<IImageProcessor<Rgba32>>();
            this.cloningProcessorImpl = new Mock<ICloningImageProcessor<Rgba32>>();
        }

        // bool throwException, bool useBounds
        public static readonly TheoryData<bool, bool> ProcessorTestData = new TheoryData<bool, bool>()
                                                                          {
                                                                              { false, false },
                                                                              { false, true },
                                                                              { true, false },
                                                                              { true, true }
                                                                          };
        [Theory]
        [MemberData(nameof(ProcessorTestData))]
        public void Mutate_RegularProcessor(bool throwException, bool useBounds)
        {
            this.SetupRegularProcessor(throwException);

            if (throwException)
            {
                Assert.Throws<ImageProcessingException>(() => this.MutateApply(useBounds));
            }
            else
            {
                this.MutateApply(useBounds);
            }

            this.regularProcessorImpl.Verify(p => p.Apply(), Times.Once());
            this.regularProcessorImpl.Verify(p => p.Dispose(), Times.Once());
        }

        [Theory]
        [MemberData(nameof(ProcessorTestData))]
        public void Clone_RegularProcessor(bool throwException, bool useBounds)
        {
            this.SetupRegularProcessor(throwException);

            if (throwException)
            {
                Assert.Throws<ImageProcessingException>(() => this.CloneApply(useBounds));
            }
            else
            {
                this.CloneApply(useBounds);
            }

            this.regularProcessorImpl.Verify(p => p.Apply(), Times.Once());
            this.regularProcessorImpl.Verify(p => p.Dispose(), Times.Once());
        }

        [Theory]
        [MemberData(nameof(ProcessorTestData))]
        public void Mutate_CloningProcessor(bool throwException, bool useBounds)
        {
            this.SetupCloningProcessor(throwException);

            if (throwException)
            {
                Assert.Throws<ImageProcessingException>(() => this.MutateApply(useBounds));
            }
            else
            {
                this.MutateApply(useBounds);
            }

            this.cloningProcessorImpl.Verify(p => p.CloneAndApply(), Times.Once());
            this.cloningProcessorImpl.Verify(p => p.Dispose(), Times.Once());
        }

        [Theory]
        [MemberData(nameof(ProcessorTestData))]
        public void Clone_CloningProcessor(bool throwException, bool useBounds)
        {
            this.SetupCloningProcessor(throwException);

            if (throwException)
            {
                Assert.Throws<ImageProcessingException>(() => this.CloneApply(useBounds));
            }
            else
            {
                this.CloneApply(useBounds);
            }

            this.cloningProcessorImpl.Verify(p => p.CloneAndApply(), Times.Once());
            this.cloningProcessorImpl.Verify(p => p.Dispose(), Times.Once());
        }

        private void MutateApply(bool useBounds)
        {
            if (useBounds)
            {
                this.image.Mutate(c => c.ApplyProcessor(this.processorDefinition.Object, Bounds));
            }
            else
            {
                this.image.Mutate(c => c.ApplyProcessor(this.processorDefinition.Object));
            }
        }

        private void CloneApply(bool useBounds)
        {
            if (useBounds)
            {
                this.image.Clone(c => c.ApplyProcessor(this.processorDefinition.Object, Bounds)).Dispose();
            }
            else
            {
                this.image.Clone(c => c.ApplyProcessor(this.processorDefinition.Object)).Dispose();
            }
        }

        private void SetupRegularProcessor(bool throwsException)
        {
            if (throwsException)
            {
                this.regularProcessorImpl.Setup(p => p.Apply()).Throws(new ImageProcessingException("Test"));
            }

            this.processorDefinition
                .Setup(p => p.CreatePixelSpecificProcessor(It.IsAny<Image<Rgba32>>(), It.IsAny<Rectangle>()))
                .Returns(this.regularProcessorImpl.Object);
        }

        private void SetupCloningProcessor(bool throwsException)
        {
            if (throwsException)
            {
                this.cloningProcessorImpl.Setup(p => p.Apply()).Throws(new ImageProcessingException("Test"));
                this.cloningProcessorImpl.Setup(p => p.CloneAndApply()).Throws(new ImageProcessingException("Test"));
            }

            this.processorDefinition
                .Setup(p => p.CreatePixelSpecificProcessor(It.IsAny<Image<Rgba32>>(), It.IsAny<Rectangle>()))
                .Returns(this.cloningProcessorImpl.Object);
        }
    }
}
