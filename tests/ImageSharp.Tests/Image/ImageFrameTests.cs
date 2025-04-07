// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests;

public class ImageFrameTests
{
    public class Indexer
    {
        private readonly Configuration configuration = Configuration.CreateDefaultInstance();

        private void LimitBufferCapacity(int bufferCapacityInBytes)
        {
            var allocator = new TestMemoryAllocator();
            allocator.BufferCapacityInBytes = bufferCapacityInBytes;
            this.configuration.MemoryAllocator = allocator;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetSet(bool enforceDisco)
        {
            if (enforceDisco)
            {
                this.LimitBufferCapacity(100);
            }

            using var image = new Image<Rgba32>(this.configuration, 10, 10);
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;
            Rgba32 val = frame[3, 4];
            Assert.Equal(default(Rgba32), val);
            frame[3, 4] = Color.Red.ToPixel<Rgba32>();
            val = frame[3, 4];
            Assert.Equal(Color.Red.ToPixel<Rgba32>(), val);
        }

        public static TheoryData<bool, int> OutOfRangeData = new TheoryData<bool, int>()
        {
            { false, -1 },
            { false, 10 },
            { true, -1 },
            { true, 10 },
        };

        [Theory]
        [MemberData(nameof(OutOfRangeData))]
        public void Get_OutOfRangeX(bool enforceDisco, int x)
        {
            if (enforceDisco)
            {
                this.LimitBufferCapacity(100);
            }

            using var image = new Image<Rgba32>(this.configuration, 10, 10);
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = frame[x, 3]);
            Assert.Equal("x", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(OutOfRangeData))]
        public void Set_OutOfRangeX(bool enforceDisco, int x)
        {
            if (enforceDisco)
            {
                this.LimitBufferCapacity(100);
            }

            using var image = new Image<Rgba32>(this.configuration, 10, 10);
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => frame[x, 3] = default);
            Assert.Equal("x", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(OutOfRangeData))]
        public void Set_OutOfRangeY(bool enforceDisco, int y)
        {
            if (enforceDisco)
            {
                this.LimitBufferCapacity(100);
            }

            using var image = new Image<Rgba32>(this.configuration, 10, 10);
            ImageFrame<Rgba32> frame = image.Frames.RootFrame;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => frame[3, y] = default);
            Assert.Equal("y", ex.ParamName);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void CopyPixelDataTo_Success(bool disco, bool byteSpan)
        {
            if (disco)
            {
                this.LimitBufferCapacity(20);
            }

            using var image = new Image<La16>(this.configuration, 10, 10);
            if (disco)
            {
                Assert.True(image.GetPixelMemoryGroup().Count > 1);
            }

            byte[] expected = TestUtils.FillImageWithRandomBytes(image);
            Span<byte> actual = new byte[expected.Length];
            if (byteSpan)
            {
                image.Frames.RootFrame.CopyPixelDataTo(actual);
            }
            else
            {
                Span<La16> destination = MemoryMarshal.Cast<byte, La16>(actual);
                image.Frames.RootFrame.CopyPixelDataTo(destination);
            }

            Assert.True(expected.AsSpan().SequenceEqual(actual));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyPixelDataTo_DestinationTooShort_Throws(bool byteSpan)
        {
            using var image = new Image<La16>(this.configuration, 10, 10);

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            {
                if (byteSpan)
                {
                    image.Frames.RootFrame.CopyPixelDataTo(new byte[199]);
                }
                else
                {
                    image.Frames.RootFrame.CopyPixelDataTo(new La16[99]);
                }
            });
        }
    }

    public class ProcessPixelRows : ProcessPixelRowsTestBase
    {
        protected override void ProcessPixelRowsImpl<TPixel>(
            Image<TPixel> image,
            PixelAccessorAction<TPixel> processPixels) =>
            image.Frames.RootFrame.ProcessPixelRows(processPixels);

        protected override void ProcessPixelRowsImpl<TPixel>(
            Image<TPixel> image1,
            Image<TPixel> image2,
            PixelAccessorAction<TPixel, TPixel> processPixels) =>
            image1.Frames.RootFrame.ProcessPixelRows(image2.Frames.RootFrame, processPixels);

        protected override void ProcessPixelRowsImpl<TPixel>(
            Image<TPixel> image1,
            Image<TPixel> image2,
            Image<TPixel> image3,
            PixelAccessorAction<TPixel, TPixel, TPixel> processPixels) =>
            image1.Frames.RootFrame.ProcessPixelRows(
                image2.Frames.RootFrame,
                image3.Frames.RootFrame,
                processPixels);

        [Fact]
        public void NullReference_Throws()
        {
            using var img = new Image<Rgb24>(1, 1);
            ImageFrame<Rgb24> frame = img.Frames.RootFrame;

            Assert.Throws<ArgumentNullException>(() => frame.ProcessPixelRows(null));

            Assert.Throws<ArgumentNullException>(() => frame.ProcessPixelRows((ImageFrame<Rgb24>)null, (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() => frame.ProcessPixelRows(frame, frame, null));

            Assert.Throws<ArgumentNullException>(() => frame.ProcessPixelRows((ImageFrame<Rgb24>)null, frame, (_, _, _) => { }));
            Assert.Throws<ArgumentNullException>(() => frame.ProcessPixelRows(frame, (ImageFrame<Rgb24>)null, (_, _, _) => { }));
            Assert.Throws<ArgumentNullException>(() => frame.ProcessPixelRows(frame, frame, null));
        }
    }
}
