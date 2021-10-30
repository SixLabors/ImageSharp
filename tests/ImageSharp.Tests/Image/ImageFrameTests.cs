// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageFrameTests
    {
        public class Indexer
        {
            private readonly Configuration configuration = Configuration.CreateDefaultInstance();

            private void LimitBufferCapacity(int bufferCapacityInBytes)
            {
                // TODO: Create a test-only MemoryAllocator for this
#pragma warning disable CS0618 // 'ArrayPoolMemoryAllocator' is obsolete
                var allocator = ArrayPoolMemoryAllocator.CreateDefault();
#pragma warning restore CS0618
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
                frame[3, 4] = Color.Red;
                val = frame[3, 4];
                Assert.Equal(Color.Red.ToRgba32(), val);
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
}
