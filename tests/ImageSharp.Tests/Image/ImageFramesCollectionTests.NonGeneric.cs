// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class ImageFramesCollectionTests
    {
        public class NonGeneric : ImageFramesCollectionTests
        {
            private new Image Image => base.Image;

            private ImageFrameCollection Collection => base.Collection;

            [Fact]
            public void ImageFramesaLwaysHaveOneFrame()
            {
                Assert.Equal(1, this.Collection.Count);
            }

            [Fact]
            public void AddNewFrame_FramesMustHaveSameSize()
            {
                ArgumentException ex = Assert.Throws<ArgumentException>(
                    () =>
                    {
                        this.Collection.AddFrame(new ImageFrame<Rgba32>(Configuration.Default, 1, 1));
                    });

                Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
            }

            [Fact]
            public void AddNewFrame_Frame_FramesNotBeNull()
            {
                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                    () =>
                    {
                        this.Collection.AddFrame((ImageFrame<Rgba32>)null);
                    });

                Assert.StartsWith("Value cannot be null.", ex.Message);
            }

            [Fact]
            public void InsertNewFrame_FramesMustHaveSameSize()
            {
                ArgumentException ex = Assert.Throws<ArgumentException>(
                    () =>
                    {
                        this.Collection.InsertFrame(1, new ImageFrame<Rgba32>(Configuration.Default, 1, 1));
                    });

                Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
            }

            [Fact]
            public void InsertNewFrame_FramesNotBeNull()
            {
                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                    () =>
                    {
                        this.Collection.InsertFrame(1, null);
                    });

                Assert.StartsWith("Value cannot be null.", ex.Message);
            }


            [Fact]
            public void RemoveAtFrame_ThrowIfRemovingLastFrame()
            {

                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                    () =>
                    {
                        this.Collection.RemoveFrame(0);
                    });
                Assert.Equal("Cannot remove last frame.", ex.Message);
            }

            [Fact]
            public void RemoveAtFrame_CanRemoveFrameZeroIfMultipleFramesExist()
            {
                this.Collection.AddFrame(new ImageFrame<Rgba32>(Configuration.Default, 10, 10));

                this.Collection.RemoveFrame(0);
                Assert.Equal(1, this.Collection.Count);
            }

            [Fact]
            public void RootFrameIsFrameAtIndexZero()
            {
                Assert.Equal(this.Collection.RootFrame, this.Collection[0]);
            }

            [Theory]
            [WithTestPatternImages(10, 10, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
            public void CloneFrame<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : struct, IPixel<TPixel>
            {
                using (Image<TPixel> img = provider.GetImage())
                {
                    ImageFrameCollection nonGenericFrameCollection = img.Frames;

                    nonGenericFrameCollection.AddFrame(new ImageFrame<TPixel>(Configuration.Default, 10, 10)); // add a frame anyway
                    using (Image cloned = nonGenericFrameCollection.CloneFrame(0))
                    {
                        Assert.Equal(2, img.Frames.Count);

                        Image<TPixel> expectedClone = (Image<TPixel>)cloned;

                        expectedClone.ComparePixelBufferTo(img.GetPixelSpan());
                    }
                }
            }

            [Theory]
            [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
            public void ExtractFrame<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : struct, IPixel<TPixel>
            {
                using (Image<TPixel> img = provider.GetImage())
                {
                    var sourcePixelData = img.GetPixelSpan().ToArray();

                    ImageFrameCollection nonGenericFrameCollection = img.Frames;

                    nonGenericFrameCollection.AddFrame(new ImageFrame<TPixel>(Configuration.Default, 10, 10));
                    using (Image cloned = nonGenericFrameCollection.ExportFrame(0))
                    {
                        Assert.Equal(1, img.Frames.Count);

                        Image<TPixel> expectedClone = (Image<TPixel>)cloned;
                        expectedClone.ComparePixelBufferTo(sourcePixelData);
                    }
                }
            }

            [Fact]
            public void CreateFrame_Default()
            {
                this.Image.Frames.CreateFrame();

                Assert.Equal(2, this.Image.Frames.Count);

                ImageFrame<Rgba32> frame = (ImageFrame<Rgba32>)this.Image.Frames[1];

                frame.ComparePixelBufferTo(default(Rgba32));
            }

            [Fact]
            public void CreateFrame_CustomFillColor()
            {
                this.Image.Frames.CreateFrame(Rgba32.HotPink);

                Assert.Equal(2, this.Image.Frames.Count);

                ImageFrame<Rgba32> frame = (ImageFrame<Rgba32>)this.Image.Frames[1];

                frame.ComparePixelBufferTo(Rgba32.HotPink);
            }

            [Fact]
            public void MoveFrame_LeavesFrmaeInCorrectLocation()
            {
                for (var i = 0; i < 9; i++)
                {
                    this.Image.Frames.CreateFrame();
                }

                var frame = this.Image.Frames[4];
                this.Image.Frames.MoveFrame(4, 7);
                var newIndex = this.Image.Frames.IndexOf(frame);
                Assert.Equal(7, newIndex);
            }

            [Fact]
            public void IndexOf_ReturnsCorrectIndex()
            {
                for (var i = 0; i < 9; i++)
                {
                    this.Image.Frames.CreateFrame();
                }

                var frame = this.Image.Frames[4];
                var index = this.Image.Frames.IndexOf(frame);
                Assert.Equal(4, index);
            }

            [Fact]
            public void Contains_TrueIfMember()
            {
                for (var i = 0; i < 9; i++)
                {
                    this.Image.Frames.CreateFrame();
                }

                var frame = this.Image.Frames[4];
                Assert.True(this.Image.Frames.Contains(frame));
            }

            [Fact]
            public void Contains_TFalseIfNoneMember()
            {
                for (var i = 0; i < 9; i++)
                {
                    this.Image.Frames.CreateFrame();
                }

                var frame = new ImageFrame<Rgba32>(Configuration.Default, 10, 10);
                Assert.False(this.Image.Frames.Contains(frame));
            }
        }
    }
}
