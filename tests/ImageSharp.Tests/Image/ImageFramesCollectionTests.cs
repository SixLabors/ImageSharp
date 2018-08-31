using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageFramesCollectionTests : IDisposable
    {
        private Image<Rgba32> image;
        private ImageFrameCollection<Rgba32> collection;

        public ImageFramesCollectionTests()
        {
            // Needed to get English exception messages, which are checked in several tests.
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");

            this.image = new Image<Rgba32>(10, 10);
            this.collection = new ImageFrameCollection<Rgba32>(this.image, 10, 10, default(Rgba32));
        }

        [Fact]
        public void ImageFramesaLwaysHaveOneFrame()
        {
            Assert.Equal(1, this.collection.Count);
        }

        [Fact]
        public void AddNewFrame_FramesMustHaveSameSize()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                this.collection.AddFrame(new ImageFrame<Rgba32>(Configuration.Default, 1, 1));
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void AddNewFrame_Frame_FramesNotBeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                this.collection.AddFrame((ImageFrame<Rgba32>)null);
            });

            Assert.StartsWith("Value cannot be null.", ex.Message);
        }

        [Fact]
        public void AddNewFrame_PixelBuffer_DataMustNotBeNull()
        {
            Rgba32[] data = null;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                this.collection.AddFrame(data);
            });

            Assert.StartsWith("Value cannot be null.", ex.Message);
        }

        [Fact]
        public void AddNewFrame_PixelBuffer_BufferIncorrectSize()
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                this.collection.AddFrame(new Rgba32[0]);
            });

            Assert.StartsWith("Value 0 must be greater than or equal to 100.", ex.Message);
        }

        [Fact]
        public void InsertNewFrame_FramesMustHaveSameSize()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                this.collection.InsertFrame(1, new ImageFrame<Rgba32>(Configuration.Default, 1, 1));
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void InsertNewFrame_FramesNotBeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                this.collection.InsertFrame(1, null);
            });

            Assert.StartsWith("Value cannot be null.", ex.Message);
        }

        [Fact]
        public void Constructor_FramesMustHaveSameSize()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                    new ImageFrame<Rgba32>(Configuration.Default,10,10),
                    new ImageFrame<Rgba32>(Configuration.Default,1,1)
                });
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void RemoveAtFrame_ThrowIfRemovingLastFrame()
        {
            var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                    new ImageFrame<Rgba32>(Configuration.Default,10,10)
                });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                collection.RemoveFrame(0);
            });
            Assert.Equal("Cannot remove last frame.", ex.Message);
        }

        [Fact]
        public void RemoveAtFrame_CanRemoveFrameZeroIfMultipleFramesExist()
        {

            var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                    new ImageFrame<Rgba32>(Configuration.Default,10,10),
                    new ImageFrame<Rgba32>(Configuration.Default,10,10)
                });

            collection.RemoveFrame(0);
            Assert.Equal(1, collection.Count);
        }

        [Fact]
        public void RootFrameIsFrameAtIndexZero()
        {
            var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                new ImageFrame<Rgba32>(Configuration.Default,10,10),
                new ImageFrame<Rgba32>(Configuration.Default,10,10)
            });

            Assert.Equal(collection.RootFrame, collection[0]);
        }

        [Fact]
        public void ConstructorPopulatesFrames()
        {
            var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                new ImageFrame<Rgba32>(Configuration.Default,10,10),
                new ImageFrame<Rgba32>(Configuration.Default,10,10)
            });

            Assert.Equal(2, collection.Count);
        }

        [Fact]
        public void DisposeClearsCollection()
        {
            var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                new ImageFrame<Rgba32>(Configuration.Default,10,10),
                new ImageFrame<Rgba32>(Configuration.Default,10,10)
            });

            collection.Dispose();

            Assert.Equal(0, collection.Count);
        }

        [Fact]
        public void Dispose_DisposesAllInnerFrames()
        {
            var collection = new ImageFrameCollection<Rgba32>(this.image, new[] {
                new ImageFrame<Rgba32>(Configuration.Default,10,10),
                new ImageFrame<Rgba32>(Configuration.Default,10,10)
            });

            IPixelSource<Rgba32>[] framesSnapShot = collection.OfType<IPixelSource<Rgba32>>().ToArray();
            collection.Dispose();

            Assert.All(framesSnapShot, f =>
            {
                // the pixel source of the frame is null after its been disposed.
                Assert.Null(f.PixelBuffer);
            });
        }

        [Theory]
        [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
        public void CloneFrame<TPixel>(TestImageProvider<TPixel> provider)
           where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                img.Frames.AddFrame(new ImageFrame<TPixel>(Configuration.Default, 10, 10));// add a frame anyway
                using (Image<TPixel> cloned = img.Frames.CloneFrame(0))
                {
                    Assert.Equal(2, img.Frames.Count);
                    cloned.ComparePixelBufferTo(img.GetPixelSpan());
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
            this.image.Frames.CreateFrame();

            Assert.Equal(2, this.image.Frames.Count);
            this.image.Frames[1].ComparePixelBufferTo(default(Rgba32));
        }

        [Fact]
        public void CreateFrame_CustomFillColor()
        {
            this.image.Frames.CreateFrame(Rgba32.HotPink);

            Assert.Equal(2, this.image.Frames.Count);
            this.image.Frames[1].ComparePixelBufferTo(Rgba32.HotPink);
        }

        [Fact]
        public void AddFrameFromPixelData()
        {
            var pixelData = this.image.Frames.RootFrame.GetPixelSpan().ToArray();
            this.image.Frames.AddFrame(pixelData);
            Assert.Equal(2, this.image.Frames.Count);
        }

        [Fact]
        public void AddFrame_clones_sourceFrame()
        {
            var pixelData = this.image.Frames.RootFrame.GetPixelSpan().ToArray();
            var otherFRame = new ImageFrame<Rgba32>(Configuration.Default, 10, 10);
            var addedFrame = this.image.Frames.AddFrame(otherFRame);
            addedFrame.ComparePixelBufferTo(otherFRame.GetPixelSpan());
            Assert.NotEqual(otherFRame, addedFrame);
        }

        [Fact]
        public void InsertFrame_clones_sourceFrame()
        {
            var pixelData = this.image.Frames.RootFrame.GetPixelSpan().ToArray();
            var otherFRame = new ImageFrame<Rgba32>(Configuration.Default, 10, 10);
            var addedFrame = this.image.Frames.InsertFrame(0, otherFRame);
            addedFrame.ComparePixelBufferTo(otherFRame.GetPixelSpan());
            Assert.NotEqual(otherFRame, addedFrame);
        }

        [Fact]
        public void MoveFrame_LeavesFrmaeInCorrectLocation()
        {
            for (var i = 0; i < 9; i++)
            {
                this.image.Frames.CreateFrame();
            }

            var frame = this.image.Frames[4];
            this.image.Frames.MoveFrame(4, 7);
            var newIndex = this.image.Frames.IndexOf(frame);
            Assert.Equal(7, newIndex);
        }


        [Fact]
        public void IndexOf_ReturnsCorrectIndex()
        {
            for (var i = 0; i < 9; i++)
            {
                this.image.Frames.CreateFrame();
            }

            var frame = this.image.Frames[4];
            var index = this.image.Frames.IndexOf(frame);
            Assert.Equal(4, index);
        }

        [Fact]
        public void Contains_TrueIfMember()
        {
            for (var i = 0; i < 9; i++)
            {
                this.image.Frames.CreateFrame();
            }

            var frame = this.image.Frames[4];
            Assert.True(this.image.Frames.Contains(frame));
        }

        [Fact]
        public void Contains_TFalseIfNoneMember()
        {
            for (var i = 0; i < 9; i++)
            {
                this.image.Frames.CreateFrame();
            }

            var frame = new ImageFrame<Rgba32>(Configuration.Default, 10, 10);
            Assert.False(this.image.Frames.Contains(frame));
        }

        public void Dispose()
        {
            this.image.Dispose();
            this.collection.Dispose();
        }
    }
}
