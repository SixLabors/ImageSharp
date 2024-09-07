// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests;

public abstract partial class ImageFrameCollectionTests
{
    [GroupOutput("ImageFramesCollectionTests")]
    public class Generic : ImageFrameCollectionTests
    {
        [Fact]
        public void Constructor_ShouldCreateOneFrame()
            => Assert.Equal(1, this.Collection.Count);

        [Fact]
        public void AddNewFrame_FramesMustHaveSameSize()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                {
                    using ImageFrame<Rgba32> frame = new(Configuration.Default, 1, 1);
                    using ImageFrame<Rgba32> addedFrame = this.Collection.AddFrame(frame);
                });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void AddNewFrame_Frame_FramesNotBeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                {
                    using ImageFrame<Rgba32> addedFrame = this.Collection.AddFrame((ImageFrame<Rgba32>)null);
                });

            Assert.StartsWith("Value cannot be null. (Parameter 'frame')", ex.Message);
        }

        [Fact]
        public void AddNewFrame_PixelBuffer_DataMustNotBeNull()
        {
            Rgba32[] data = null;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                {
                    using ImageFrame<Rgba32> addedFrame = this.Collection.AddFrame(data);
                });

            Assert.StartsWith("Value cannot be null. (Parameter 'source')", ex.Message);
        }

        [Fact]
        public void AddNewFrame_PixelBuffer_BufferIncorrectSize()
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                {
                    using ImageFrame<Rgba32> addedFrame = this.Collection.AddFrame(Array.Empty<Rgba32>());
                });

            Assert.StartsWith($"Parameter \"data\" ({typeof(int)}) must be greater than or equal to {100}, was {0}", ex.Message);
        }

        [Fact]
        public void InsertNewFrame_FramesMustHaveSameSize()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                {
                    using ImageFrame<Rgba32> frame = new(Configuration.Default, 1, 1);
                    using ImageFrame<Rgba32> insertedFrame = this.Collection.InsertFrame(1, frame);
                });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void InsertNewFrame_FramesNotBeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                {
                    using ImageFrame<Rgba32> insertedFrame = this.Collection.InsertFrame(1, null);
                });

            Assert.StartsWith("Value cannot be null. (Parameter 'frame')", ex.Message);
        }

        [Fact]
        public void Constructor_FramesMustHaveSameSize()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                {
                    using ImageFrame<Rgba32> imageFrame1 = new(Configuration.Default, 10, 10);
                    using ImageFrame<Rgba32> imageFrame2 = new(Configuration.Default, 1, 1);
                    new ImageFrameCollection<Rgba32>(
                        this.Image,
                        [imageFrame1, imageFrame2]);
                });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void RemoveAtFrame_ThrowIfRemovingLastFrame()
        {
            using ImageFrame<Rgba32> imageFrame = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> collection = new(
                this.Image,
                [imageFrame]);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => collection.RemoveFrame(0));
            Assert.Equal("Cannot remove last frame.", ex.Message);
        }

        [Fact]
        public void RemoveAtFrame_CanRemoveFrameZeroIfMultipleFramesExist()
        {
            using ImageFrame<Rgba32> imageFrame1 = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> imageFrame2 = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> collection = new(
                this.Image,
                [imageFrame1, imageFrame2]);

            collection.RemoveFrame(0);
            Assert.Equal(1, collection.Count);
        }

        [Fact]
        public void RootFrameIsFrameAtIndexZero()
        {
            using ImageFrame<Rgba32> imageFrame1 = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> imageFrame2 = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> collection = new(
                this.Image,
                [imageFrame1, imageFrame2]);

            Assert.Equal(collection.RootFrame, collection[0]);
        }

        [Fact]
        public void ConstructorPopulatesFrames()
        {
            using ImageFrame<Rgba32> imageFrame1 = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> imageFrame2 = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> collection = new(
                this.Image,
                [imageFrame1, imageFrame2]);

            Assert.Equal(2, collection.Count);
        }

        [Fact]
        public void DisposeClearsCollection()
        {
            using ImageFrame<Rgba32> imageFrame1 = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> imageFrame2 = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> collection = new(
                this.Image,
                [imageFrame1, imageFrame2]);

            collection.Dispose();

            Assert.Equal(0, collection.Count);
        }

        [Fact]
        public void Dispose_DisposesAllInnerFrames()
        {
            using ImageFrame<Rgba32> imageFrame1 = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> imageFrame2 = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> collection = new(
                this.Image,
                [imageFrame1, imageFrame2]);

            IPixelSource<Rgba32>[] framesSnapShot = collection.OfType<IPixelSource<Rgba32>>().ToArray();

            Assert.All(framesSnapShot, f => Assert.False(f.PixelBuffer.IsDisposed));

            collection.Dispose();

            Assert.All(framesSnapShot, f => Assert.True(f.PixelBuffer.IsDisposed));
        }

        [Theory]
        [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
        public void CloneFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> img = provider.GetImage();
            using ImageFrame<Rgba32> imageFrame = new(Configuration.Default, 10, 10);
            using ImageFrame addedFrame = img.Frames.AddFrame(imageFrame); // add a frame anyway
            using Image<TPixel> cloned = img.Frames.CloneFrame(0);
            Assert.Equal(2, img.Frames.Count);
            Assert.True(img.DangerousTryGetSinglePixelMemory(out Memory<TPixel> imgMem));

            cloned.ComparePixelBufferTo(imgMem);
        }

        [Theory]
        [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
        public void ExtractFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> img = provider.GetImage();
            Assert.True(img.DangerousTryGetSinglePixelMemory(out Memory<TPixel> imgMemory));
            TPixel[] sourcePixelData = imgMemory.ToArray();

            using ImageFrame<Rgba32> imageFrame = new(Configuration.Default, 10, 10);
            using ImageFrame addedFrame = img.Frames.AddFrame(imageFrame);
            using Image<TPixel> cloned = img.Frames.ExportFrame(0);
            Assert.Equal(1, img.Frames.Count);
            cloned.ComparePixelBufferTo(sourcePixelData.AsSpan());
        }

        [Fact]
        public void CreateFrame_Default()
        {
            using (this.Image.Frames.CreateFrame())
            {
                Assert.Equal(2, this.Image.Frames.Count);
                this.Image.Frames[1].ComparePixelBufferTo(default(Rgba32));
            }
        }

        [Fact]
        public void CreateFrame_CustomFillColor()
        {
            using (this.Image.Frames.CreateFrame(Color.HotPink))
            {
                Assert.Equal(2, this.Image.Frames.Count);
                this.Image.Frames[1].ComparePixelBufferTo(Color.HotPink.ToPixel<Rgba32>());
            }
        }

        [Fact]
        public void AddFrameFromPixelData()
        {
            Assert.True(this.Image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imgMem));
            Rgba32[] pixelData = imgMem.ToArray();
            using ImageFrame<Rgba32> addedFrame = this.Image.Frames.AddFrame(pixelData);
            Assert.Equal(2, this.Image.Frames.Count);
        }

        [Fact]
        public void AddFrame_clones_sourceFrame()
        {
            using ImageFrame<Rgba32> otherFrame = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> addedFrame = this.Image.Frames.AddFrame(otherFrame);

            Assert.True(otherFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> otherFrameMem));
            addedFrame.ComparePixelBufferTo(otherFrameMem.Span);
            Assert.NotEqual(otherFrame, addedFrame);
        }

        [Fact]
        public void InsertFrame_clones_sourceFrame()
        {
            using ImageFrame<Rgba32> otherFrame = new(Configuration.Default, 10, 10);
            using ImageFrame<Rgba32> addedFrame = this.Image.Frames.InsertFrame(0, otherFrame);

            Assert.True(otherFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> otherFrameMem));
            addedFrame.ComparePixelBufferTo(otherFrameMem.Span);
            Assert.NotEqual(otherFrame, addedFrame);
        }

        [Fact]
        public void MoveFrame_LeavesFrameInCorrectLocation()
        {
            for (int i = 0; i < 9; i++)
            {
                this.Image.Frames.CreateFrame();
            }

            ImageFrame<Rgba32> frame = this.Image.Frames[4];
            this.Image.Frames.MoveFrame(4, 7);
            int newIndex = this.Image.Frames.IndexOf(frame);
            Assert.Equal(7, newIndex);
        }

        [Fact]
        public void IndexOf_ReturnsCorrectIndex()
        {
            for (int i = 0; i < 9; i++)
            {
                this.Image.Frames.CreateFrame();
            }

            ImageFrame<Rgba32> frame = this.Image.Frames[4];
            int index = this.Image.Frames.IndexOf(frame);
            Assert.Equal(4, index);
        }

        [Fact]
        public void Contains_TrueIfMember()
        {
            for (int i = 0; i < 9; i++)
            {
                this.Image.Frames.CreateFrame();
            }

            ImageFrame<Rgba32> frame = this.Image.Frames[4];
            Assert.True(this.Image.Frames.Contains(frame));
        }

        [Fact]
        public void Contains_FalseIfNonMember()
        {
            for (int i = 0; i < 9; i++)
            {
                this.Image.Frames.CreateFrame();
            }

            using ImageFrame<Rgba32> frame = new(Configuration.Default, 10, 10);
            Assert.False(this.Image.Frames.Contains(frame));
        }

        [Fact]
        public void PreferContiguousImageBuffers_True_AppliedToAllFrames()
        {
            Configuration configuration = Configuration.Default.Clone();
            configuration.MemoryAllocator = new TestMemoryAllocator { BufferCapacityInBytes = 1000 };
            configuration.PreferContiguousImageBuffers = true;

            using Image<Rgba32> image = new(configuration, 100, 100);
            image.Frames.CreateFrame();
            image.Frames.InsertFrame(0, image.Frames[0]);
            image.Frames.CreateFrame(Color.Red);

            Assert.Equal(4, image.Frames.Count);
            foreach (ImageFrame<Rgba32> frame in image.Frames)
            {
                Assert.True(frame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> _));
            }
        }

        [Fact]
        public void DisposeCall_NoThrowIfCalledMultiple()
        {
            Image<Rgba32> image = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> frameCollection = image.Frames;

            image.Dispose(); // this should invalidate underlying collection as well
            frameCollection.Dispose();
        }

        [Fact]
        public void PublicProperties_ThrowIfDisposed()
        {
            Image<Rgba32> image = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> frameCollection = image.Frames;

            image.Dispose(); // this should invalidate underlying collection as well

            Assert.Throws<ObjectDisposedException>(() => { ImageFrame prop = frameCollection.RootFrame; });
        }

        [Fact]
        public void PublicMethods_ThrowIfDisposed()
        {
            Image<Rgba32> image = new(Configuration.Default, 10, 10);
            ImageFrameCollection<Rgba32> frameCollection = image.Frames;

            image.Dispose(); // this should invalidate underlying collection as well

            Assert.Throws<ObjectDisposedException>(() => { ImageFrame<Rgba32> res = frameCollection.AddFrame(default(ImageFrame<Rgba32>)); });
            Assert.Throws<ObjectDisposedException>(() => { Image<Rgba32> res = frameCollection.CloneFrame(default); });
            Assert.Throws<ObjectDisposedException>(() => { bool res = frameCollection.Contains(default); });
            Assert.Throws<ObjectDisposedException>(() => { ImageFrame<Rgba32> res = frameCollection.CreateFrame(); });
            Assert.Throws<ObjectDisposedException>(() => { ImageFrame<Rgba32> res = frameCollection.CreateFrame(default); });
            Assert.Throws<ObjectDisposedException>(() => { Image<Rgba32> res = frameCollection.ExportFrame(default); });
            Assert.Throws<ObjectDisposedException>(() => { IEnumerator<ImageFrame<Rgba32>> res = frameCollection.GetEnumerator(); });
            Assert.Throws<ObjectDisposedException>(() => { int prop = frameCollection.IndexOf(default); });
            Assert.Throws<ObjectDisposedException>(() => { ImageFrame<Rgba32> prop = frameCollection.InsertFrame(default, default); });
            Assert.Throws<ObjectDisposedException>(() => frameCollection.RemoveFrame(default));
            Assert.Throws<ObjectDisposedException>(() => frameCollection.MoveFrame(default, default));
        }
    }
}
