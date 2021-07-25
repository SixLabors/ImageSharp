// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public partial class ImageTests
    {
        public class Constructor
        {
            [Fact]
            public void Width_Height()
            {
                using (var image = new Image<Rgba32>(11, 23))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.True(image.TryGetSinglePixelSpan(out Span<Rgba32> imageSpan));
                    Assert.Equal(11 * 23, imageSpan.Length);
                    image.ComparePixelBufferTo(default(Rgba32));

                    Assert.Equal(Configuration.Default, image.GetConfiguration());
                }
            }

            [Fact]
            public void Configuration_Width_Height()
            {
                Configuration configuration = Configuration.Default.Clone();

                using (var image = new Image<Rgba32>(configuration, 11, 23))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.True(image.TryGetSinglePixelSpan(out Span<Rgba32> imageSpan));
                    Assert.Equal(11 * 23, imageSpan.Length);
                    image.ComparePixelBufferTo(default(Rgba32));

                    Assert.Equal(configuration, image.GetConfiguration());
                }
            }

            [Fact]
            public void Configuration_Width_Height_BackgroundColor()
            {
                Configuration configuration = Configuration.Default.Clone();
                Rgba32 color = Color.Aquamarine;

                using (var image = new Image<Rgba32>(configuration, 11, 23, color))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.True(image.TryGetSinglePixelSpan(out Span<Rgba32> imageSpan));
                    Assert.Equal(11 * 23, imageSpan.Length);
                    image.ComparePixelBufferTo(color);

                    Assert.Equal(configuration, image.GetConfiguration());
                }
            }

            [Fact]
            public void CreateUninitialized()
            {
                Configuration configuration = Configuration.Default.Clone();

                byte dirtyValue = 123;
                configuration.MemoryAllocator = new TestMemoryAllocator(dirtyValue);
                var metadata = new ImageMetadata();

                using (var image = Image.CreateUninitialized<L8>(configuration, 21, 22, metadata))
                {
                    Assert.Equal(21, image.Width);
                    Assert.Equal(22, image.Height);
                    Assert.Same(configuration, image.GetConfiguration());
                    Assert.Same(metadata, image.Metadata);

                    Assert.Equal(dirtyValue, image[5, 5].PackedValue);
                }
            }
        }

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
                Rgba32 val = image[3, 4];
                Assert.Equal(default(Rgba32), val);
                image[3, 4] = Color.Red;
                val = image[3, 4];
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
                ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = image[x, 3]);
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
                ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => image[x, 3] = default);
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
                ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => image[3, y] = default);
                Assert.Equal("y", ex.ParamName);
            }
        }

        public class Dispose
        {
            private readonly Configuration configuration = Configuration.CreateDefaultInstance();

            public void MultipleDisposeCalls()
            {
                var image = new Image<Rgba32>(this.configuration, 10, 10);
                image.Dispose();
                image.Dispose();
            }

            [Fact]
            public void NonPrivateProperties_ObjectDisposedException()
            {
                var image = new Image<Rgba32>(this.configuration, 10, 10);
                var genericImage = (Image)image;

                image.Dispose();

                // Image<TPixel>
                Assert.Throws<ObjectDisposedException>(() => { var prop = image.Frames; });

                // Image
                Assert.Throws<ObjectDisposedException>(() => { var prop = genericImage.Frames; });
            }

            [Fact]
            public void Save_ObjectDisposedException()
            {
                using var stream = new MemoryStream();
                var image = new Image<Rgba32>(this.configuration, 10, 10);
                var encoder = new JpegEncoder();

                image.Dispose();

                // Image<TPixel>
                Assert.Throws<ObjectDisposedException>(() => image.Save(stream, encoder));
            }

            [Fact]
            public void AcceptVisitor_ObjectDisposedException()
            {
                // This test technically should exist but it's impossible to write proper test case without reflection:
                // All visitor types are private and can't be created without context of some save/processing operation
                // Save_ObjectDisposedException test checks this method with AcceptVisitor(EncodeVisitor) anyway
                return;
            }

            [Fact]
            public void NonPrivateMethods_ObjectDisposedException()
            {
                var image = new Image<Rgba32>(this.configuration, 10, 10);
                var genericImage = (Image)image;

                image.Dispose();

                // Image<TPixel>
                Assert.Throws<ObjectDisposedException>(() => { var res = image.Clone(this.configuration); });
                Assert.Throws<ObjectDisposedException>(() => { var res = image.CloneAs<Rgba32>(this.configuration); });
                Assert.Throws<ObjectDisposedException>(() => { var res = image.GetPixelRowSpan(default); });
                Assert.Throws<ObjectDisposedException>(() => { var res = image.TryGetSinglePixelSpan(out var _); });

                // Image
                Assert.Throws<ObjectDisposedException>(() => { var res = genericImage.CloneAs<Rgba32>(this.configuration); });
            }
        }
    }
}
