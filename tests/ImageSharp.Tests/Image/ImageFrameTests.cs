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
                var allocator = (ArrayPoolMemoryAllocator)this.configuration.MemoryAllocator;
                allocator.BufferCapacityInBytes = bufferCapacityInBytes;
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
    }
}
