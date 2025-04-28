// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

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
            using (Image<Rgba32> image = new(11, 23))
            {
                Assert.Equal(11, image.Width);
                Assert.Equal(23, image.Height);
                Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMem));
                Assert.Equal(11 * 23, imageMem.Length);
                image.ComparePixelBufferTo(default(Rgba32));

                Assert.Equal(Configuration.Default, image.Configuration);
            }
        }

        [Fact]
        public void Width_Height_SizeNotRepresentable_ThrowsInvalidImageOperationException()
            => Assert.Throws<InvalidMemoryOperationException>(() => new Image<Rgba32>(int.MaxValue, int.MaxValue));

        [Fact]
        public void Configuration_Width_Height()
        {
            Configuration configuration = Configuration.Default.Clone();

            using (Image<Rgba32> image = new(configuration, 11, 23))
            {
                Assert.Equal(11, image.Width);
                Assert.Equal(23, image.Height);
                Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMem));
                Assert.Equal(11 * 23, imageMem.Length);
                image.ComparePixelBufferTo(default(Rgba32));

                Assert.Equal(configuration, image.Configuration);
            }
        }

        [Fact]
        public void Configuration_Width_Height_BackgroundColor()
        {
            Configuration configuration = Configuration.Default.Clone();
            Rgba32 color = Color.Aquamarine.ToPixel<Rgba32>();

            using (Image<Rgba32> image = new(configuration, 11, 23, color))
            {
                Assert.Equal(11, image.Width);
                Assert.Equal(23, image.Height);
                Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMem));
                Assert.Equal(11 * 23, imageMem.Length);
                image.ComparePixelBufferTo(color);

                Assert.Equal(configuration, image.Configuration);
            }
        }

        [Fact]
        public void CreateUninitialized()
        {
            Configuration configuration = Configuration.Default.Clone();

            byte dirtyValue = 123;
            configuration.MemoryAllocator = new TestMemoryAllocator(dirtyValue);
            ImageMetadata metadata = new();

            using (Image<L8> image = Image.CreateUninitialized<L8>(configuration, 21, 22, metadata))
            {
                Assert.Equal(21, image.Width);
                Assert.Equal(22, image.Height);
                Assert.Same(configuration, image.Configuration);
                Assert.Same(metadata, image.Metadata);

                Assert.Equal(dirtyValue, image[5, 5].PackedValue);
            }
        }
    }

    public class Indexer
    {
        private readonly Configuration configuration = Configuration.CreateDefaultInstance();

        private void LimitBufferCapacity(int bufferCapacityInBytes) =>
            this.configuration.MemoryAllocator = new TestMemoryAllocator { BufferCapacityInBytes = bufferCapacityInBytes };

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetSet(bool enforceDisco)
        {
            if (enforceDisco)
            {
                this.LimitBufferCapacity(100);
            }

            using Image<Rgba32> image = new(this.configuration, 10, 10);
            Rgba32 val = image[3, 4];
            Assert.Equal(default(Rgba32), val);
            image[3, 4] = Color.Red.ToPixel<Rgba32>();
            val = image[3, 4];
            Assert.Equal(Color.Red.ToPixel<Rgba32>(), val);
        }

        public static TheoryData<bool, int> OutOfRangeData = new()
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

            using Image<Rgba32> image = new(this.configuration, 10, 10);
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

            using Image<Rgba32> image = new(this.configuration, 10, 10);
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

            using Image<Rgba32> image = new(this.configuration, 10, 10);
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => image[3, y] = default);
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

            using Image<La16> image = new(this.configuration, 10, 10);
            if (disco)
            {
                Assert.True(image.GetPixelMemoryGroup().Count > 1);
            }

            byte[] expected = TestUtils.FillImageWithRandomBytes(image);
            Span<byte> actual = new byte[expected.Length];
            if (byteSpan)
            {
                image.CopyPixelDataTo(actual);
            }
            else
            {
                Span<La16> destination = MemoryMarshal.Cast<byte, La16>(actual.AsSpan());
                image.CopyPixelDataTo(destination);
            }

            Assert.True(expected.AsSpan().SequenceEqual(actual));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CopyPixelDataTo_DestinationTooShort_Throws(bool byteSpan)
        {
            using Image<La16> image = new(this.configuration, 10, 10);

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            {
                if (byteSpan)
                {
                    image.CopyPixelDataTo(new byte[199]);
                }
                else
                {
                    image.CopyPixelDataTo(new La16[99]);
                }
            });
        }
    }

    public class ProcessPixelRows : ProcessPixelRowsTestBase
    {
        protected override void ProcessPixelRowsImpl<TPixel>(
            Image<TPixel> image,
            PixelAccessorAction<TPixel> processPixels) =>
            image.ProcessPixelRows(processPixels);

        protected override void ProcessPixelRowsImpl<TPixel>(
            Image<TPixel> image1,
            Image<TPixel> image2,
            PixelAccessorAction<TPixel, TPixel> processPixels) =>
            image1.ProcessPixelRows(image2, processPixels);

        protected override void ProcessPixelRowsImpl<TPixel>(
            Image<TPixel> image1,
            Image<TPixel> image2,
            Image<TPixel> image3,
            PixelAccessorAction<TPixel, TPixel, TPixel> processPixels) =>
            image1.ProcessPixelRows(image2, image3, processPixels);

        [Fact]
        public void NullReference_Throws()
        {
            using Image<Rgb24> img = new(1, 1);

            Assert.Throws<ArgumentNullException>(() => img.ProcessPixelRows(null));

            Assert.Throws<ArgumentNullException>(() => img.ProcessPixelRows((Image<Rgb24>)null, (_, _) => { }));
            Assert.Throws<ArgumentNullException>(() => img.ProcessPixelRows(img, img, null));

            Assert.Throws<ArgumentNullException>(() => img.ProcessPixelRows((Image<Rgb24>)null, img, (_, _, _) => { }));
            Assert.Throws<ArgumentNullException>(() => img.ProcessPixelRows(img, (Image<Rgb24>)null, (_, _, _) => { }));
            Assert.Throws<ArgumentNullException>(() => img.ProcessPixelRows(img, img, null));
        }
    }

    public class Dispose
    {
        private readonly Configuration configuration = Configuration.CreateDefaultInstance();

        public void MultipleDisposeCalls()
        {
            Image<Rgba32> image = new(this.configuration, 10, 10);
            image.Dispose();
            image.Dispose();
        }

        [Fact]
        public void NonPrivateProperties_ObjectDisposedException()
        {
            Image<Rgba32> image = new(this.configuration, 10, 10);
            Image genericImage = (Image)image;

            image.Dispose();

            // Image<TPixel>
            Assert.Throws<ObjectDisposedException>(() => { ImageFrameCollection<Rgba32> prop = image.Frames; });

            // Image
            Assert.Throws<ObjectDisposedException>(() => { ImageFrameCollection prop = genericImage.Frames; });
        }

        [Fact]
        public void Save_ObjectDisposedException()
        {
            using MemoryStream stream = new();
            Image<Rgba32> image = new(this.configuration, 10, 10);
            JpegEncoder encoder = new();

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
            Image<Rgba32> image = new(this.configuration, 10, 10);
            Image genericImage = (Image)image;

            image.Dispose();

            // Image<TPixel>
            Assert.Throws<ObjectDisposedException>(() => { Image<Rgba32> res = image.Clone(this.configuration); });
            Assert.Throws<ObjectDisposedException>(() => { Image<Rgba32> res = image.CloneAs<Rgba32>(this.configuration); });
            Assert.Throws<ObjectDisposedException>(() => { bool res = image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> _); });

            // Image
            Assert.Throws<ObjectDisposedException>(() => { Image<Rgba32> res = genericImage.CloneAs<Rgba32>(this.configuration); });
        }
    }

    public class DetectEncoder
    {
        [Fact]
        public void KnownExtension_ReturnsEncoder()
        {
            using Image<L8> image = new(1, 1);
            IImageEncoder encoder = image.DetectEncoder("dummy.png");
            Assert.NotNull(encoder);
            Assert.IsType<PngEncoder>(encoder);
        }

        [Fact]
        public void UnknownExtension_ThrowsUnknownImageFormatException()
        {
            using Image<L8> image = new(1, 1);
            Assert.Throws<UnknownImageFormatException>(() => image.DetectEncoder("dummy.yolo"));
        }

        [Fact]
        public void NoDetectorRegisteredForKnownExtension_ThrowsUnknownImageFormatException()
        {
            Configuration configuration = new();
            TestFormat format = new();
            configuration.ImageFormatsManager.AddImageFormat(format);
            configuration.ImageFormatsManager.AddImageFormatDetector(new MockImageFormatDetector(format));

            using Image<L8> image = new(configuration, 1, 1);
            Assert.Throws<UnknownImageFormatException>(() => image.DetectEncoder($"dummy.{format.Extension}"));
        }
    }
}
