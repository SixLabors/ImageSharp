using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageFramesCollectionTests
    {
        [Fact]
        public void ImageFramesaLwaysHaveOneFrame()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);
            Assert.Equal(1, collection.Count);
        }

        [Fact]
        public void AddNewFrame_FramesMustHaveSameSize()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                collection.Add(new ImageFrame<Rgba32>(1, 1));
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void AddNewFrame_FramesNotBeNull()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                collection.Add(null);
            });

            Assert.StartsWith("Value cannot be null.", ex.Message);
        }

        [Fact]
        public void InsertNewFrame_FramesMustHaveSameSize()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                collection.Insert(1, new ImageFrame<Rgba32>(1, 1));
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void InsertNewFrame_FramesNotBeNull()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                collection.Insert(1, null);
            });

            Assert.StartsWith("Value cannot be null.", ex.Message);
        }

        [Fact]
        public void SetFrameAtIndex_FramesMustHaveSameSize()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                collection[0] = new ImageFrame<Rgba32>(1, 1);
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void SetFrameAtIndex_FramesNotBeNull()
        {
            var collection = new ImageFrameCollection<Rgba32>(10, 10);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                collection[0] = null;
            });

            Assert.StartsWith("Value cannot be null.", ex.Message);
        }

        [Fact]
        public void Constructor_FramesMustHaveSameSize()
        {

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                var collection = new ImageFrameCollection<Rgba32>(new[] {
                    new ImageFrame<Rgba32>(10,10),
                    new ImageFrame<Rgba32>(1,1),
                });
            });

            Assert.StartsWith("Frame must have the same dimensions as the image.", ex.Message);
        }

        [Fact]
        public void RemoveAtFrame_ThrowIfRemovingLastFrame()
        {
            var collection = new ImageFrameCollection<Rgba32>(new[] {
                    new ImageFrame<Rgba32>(10,10)
                });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                collection.RemoveAt(0);
            });
            Assert.Equal("Cannot remove last frame.", ex.Message);
        }

        [Fact]
        public void RemoveAtFrame_CanRemoveFrameZeroIfMultipleFramesExist()
        {

            var collection = new ImageFrameCollection<Rgba32>(new[] {
                    new ImageFrame<Rgba32>(10,10),
                    new ImageFrame<Rgba32>(10,10),
                });

            collection.RemoveAt(0);
            Assert.Equal(1, collection.Count);
        }

        [Fact]
        public void RemoveFrame_ThrowIfRemovingLastFrame()
        {
            var collection = new ImageFrameCollection<Rgba32>(new[] {
                    new ImageFrame<Rgba32>(10,10)
                });

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                collection.Remove(collection[0]);
            });
            Assert.Equal("Cannot remove last frame.", ex.Message);
        }

        [Fact]
        public void RemoveFrame_CanRemoveFrameZeroIfMultipleFramesExist()
        {

            var collection = new ImageFrameCollection<Rgba32>(new[] {
                    new ImageFrame<Rgba32>(10,10),
                    new ImageFrame<Rgba32>(10,10),
                });

            collection.Remove(collection[0]);
            Assert.Equal(1, collection.Count);
        }

        [Fact]
        public void RootFrameIsFrameAtIndexZero()
        {
            var collection = new ImageFrameCollection<Rgba32>(new[] {
                new ImageFrame<Rgba32>(10,10),
                new ImageFrame<Rgba32>(10,10),
            });

            Assert.Equal(collection.RootFrame, collection[0]);
        }

        [Fact]
        public void ConstructorPopulatesFrames()
        {
            var collection = new ImageFrameCollection<Rgba32>(new[] {
                new ImageFrame<Rgba32>(10,10),
                new ImageFrame<Rgba32>(10,10),
            });

            Assert.Equal(2, collection.Count);
        }

        [Fact]
        public void DisposeClearsCollection()
        {
            var collection = new ImageFrameCollection<Rgba32>(new[] {
                new ImageFrame<Rgba32>(10,10),
                new ImageFrame<Rgba32>(10,10),
            });

            collection.Dispose();

            Assert.Equal(0, collection.Count);
        }

        [Fact]
        public void Dispose_DisposesAllInnerFrames()
        {
            var collection = new ImageFrameCollection<Rgba32>(new[] {
                new ImageFrame<Rgba32>(10,10),
                new ImageFrame<Rgba32>(10,10),
            });

            IPixelSource<Rgba32>[] framesSnapShot = collection.OfType<IPixelSource<Rgba32>>().ToArray();
            collection.Dispose();

            Assert.All(framesSnapShot, f =>
            {
                // the pixel source of the frame is null after its been disposed.
                Assert.Null(f.PixelBuffer);
            });
        }
    }
}
