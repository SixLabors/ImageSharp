// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class ImageFrameCollectionTests
    {
        [GroupOutput("ImageFramesCollectionTests")]
        public class Generic : ImageFrameCollectionTests
        {
            [Fact]
            public void Constructor_ShouldCreateOneFrame()
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

                Assert.StartsWith("Parameter \"frame\" must be not null.", ex.Message);
            }

            [Fact]
            public void AddNewFrame_PixelBuffer_DataMustNotBeNull()
            {
                Rgba32[] data = null;

                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                    () =>
                    {
                        this.Collection.AddFrame(data);
                    });

                Assert.StartsWith("Parameter \"source\" must be not null.", ex.Message);
            }

            [Fact]
            public void AddNewFrame_PixelBuffer_BufferIncorrectSize()
            {
                ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                    () =>
                    {
                        this.Collection.AddFrame(new Rgba32[0]);
                    });

                Assert.StartsWith($"Parameter \"data\" ({typeof(int)}) must be greater than or equal to {100}, was {0}", ex.Message);
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

                Assert.StartsWith("Parameter \"frame\" must be not null.", ex.Message);
            }

            [Fact]
            public void Constructor_FramesMustHaveSameSize()
            {
                ArgumentException ex = Assert.Throws<ArgumentException>(
                    () =>
                    {
                        new ImageFrameCollection<Rgba32>(
                            this.Image,
                            new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10), new ImageFrame<Rgba32>(Configuration.Default, 1, 1) });
                    });

                Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
            }

            [Fact]
            public void RemoveAtFrame_ThrowIfRemovingLastFrame()
            {
                var collection = new ImageFrameCollection<Rgba32>(
                    this.Image,
                    new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10) });

                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                    () =>
                    {
                        collection.RemoveFrame(0);
                    });
                Assert.Equal("Cannot remove last frame.", ex.Message);
            }

            [Fact]
            public void RemoveAtFrame_CanRemoveFrameZeroIfMultipleFramesExist()
            {
                var collection = new ImageFrameCollection<Rgba32>(
                    this.Image,
                    new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10), new ImageFrame<Rgba32>(Configuration.Default, 10, 10) });

                collection.RemoveFrame(0);
                Assert.Equal(1, collection.Count);
            }

            [Fact]
            public void RootFrameIsFrameAtIndexZero()
            {
                var collection = new ImageFrameCollection<Rgba32>(
                    this.Image,
                    new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10), new ImageFrame<Rgba32>(Configuration.Default, 10, 10) });

                Assert.Equal(collection.RootFrame, collection[0]);
            }

            [Fact]
            public void ConstructorPopulatesFrames()
            {
                var collection = new ImageFrameCollection<Rgba32>(
                    this.Image,
                    new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10), new ImageFrame<Rgba32>(Configuration.Default, 10, 10) });

                Assert.Equal(2, collection.Count);
            }

            [Fact]
            public void DisposeClearsCollection()
            {
                var collection = new ImageFrameCollection<Rgba32>(
                    this.Image,
                    new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10), new ImageFrame<Rgba32>(Configuration.Default, 10, 10) });

                collection.Dispose();

                Assert.Equal(0, collection.Count);
            }

            [Fact]
            public void Dispose_DisposesAllInnerFrames()
            {
                var collection = new ImageFrameCollection<Rgba32>(
                    this.Image,
                    new[] { new ImageFrame<Rgba32>(Configuration.Default, 10, 10), new ImageFrame<Rgba32>(Configuration.Default, 10, 10) });

                IPixelSource<Rgba32>[] framesSnapShot = collection.OfType<IPixelSource<Rgba32>>().ToArray();
                collection.Dispose();

                Assert.All(
                    framesSnapShot,
                    f =>
                    {
                        // The pixel source of the frame is null after its been disposed.
                        Assert.Null(f.PixelBuffer);
                    });
            }

            [Theory]
            [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
            public void CloneFrame<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                using (Image<TPixel> img = provider.GetImage())
                {
                    img.Frames.AddFrame(new ImageFrame<TPixel>(Configuration.Default, 10, 10)); // add a frame anyway
                    using (Image<TPixel> cloned = img.Frames.CloneFrame(0))
                    {
                        Assert.Equal(2, img.Frames.Count);
                        Assert.True(img.TryGetSinglePixelSpan(out Span<TPixel> imgSpan));

                        cloned.ComparePixelBufferTo(imgSpan);
                    }
                }
            }

            [Theory]
            [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
            public void ExtractFrame<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                using (Image<TPixel> img = provider.GetImage())
                {
                    Assert.True(img.TryGetSinglePixelSpan(out Span<TPixel> imgSpan));
                    TPixel[] sourcePixelData = imgSpan.ToArray();

                    img.Frames.AddFrame(new ImageFrame<TPixel>(Configuration.Default, 10, 10));
                    using (Image<TPixel> cloned = img.Frames.ExportFrame(0))
                    {
                        Assert.Equal(1, img.Frames.Count);
                        cloned.ComparePixelBufferTo(sourcePixelData);
                    }
                }
            }

            [Fact]
            public void CreateFrame_Default()
            {
                this.Image.Frames.CreateFrame();

                Assert.Equal(2, this.Image.Frames.Count);
                this.Image.Frames[1].ComparePixelBufferTo(default(Rgba32));
            }

            [Fact]
            public void CreateFrame_CustomFillColor()
            {
                this.Image.Frames.CreateFrame(Color.HotPink);

                Assert.Equal(2, this.Image.Frames.Count);
                this.Image.Frames[1].ComparePixelBufferTo(Color.HotPink);
            }

            [Fact]
            public void AddFrameFromPixelData()
            {
                Assert.True(this.Image.Frames.RootFrame.TryGetSinglePixelSpan(out Span<Rgba32> imgSpan));
                var pixelData = imgSpan.ToArray();
                this.Image.Frames.AddFrame(pixelData);
                Assert.Equal(2, this.Image.Frames.Count);
            }

            [Fact]
            public void AddFrame_clones_sourceFrame()
            {
                var otherFrame = new ImageFrame<Rgba32>(Configuration.Default, 10, 10);
                ImageFrame<Rgba32> addedFrame = this.Image.Frames.AddFrame(otherFrame);

                Assert.True(otherFrame.TryGetSinglePixelSpan(out Span<Rgba32> otherFrameSpan));
                addedFrame.ComparePixelBufferTo(otherFrameSpan);
                Assert.NotEqual(otherFrame, addedFrame);
            }

            [Fact]
            public void InsertFrame_clones_sourceFrame()
            {
                var otherFrame = new ImageFrame<Rgba32>(Configuration.Default, 10, 10);
                ImageFrame<Rgba32> addedFrame = this.Image.Frames.InsertFrame(0, otherFrame);

                Assert.True(otherFrame.TryGetSinglePixelSpan(out Span<Rgba32> otherFrameSpan));
                addedFrame.ComparePixelBufferTo(otherFrameSpan);
                Assert.NotEqual(otherFrame, addedFrame);
            }

            [Fact]
            public void MoveFrame_LeavesFrameInCorrectLocation()
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
            public void Contains_FalseIfNonMember()
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
