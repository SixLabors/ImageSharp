// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class WrapMemory
    {
        /// <summary>
        /// A <see cref="MemoryManager{T}"/> exposing the locked pixel memory of a <see cref="Bitmap"/> instance.
        /// TODO: This should be an example in https://github.com/SixLabors/Samples
        /// </summary>
        public class BitmapMemoryManager : MemoryManager<Bgra32>
        {
            private readonly Bitmap bitmap;

            private readonly BitmapData bmpData;

            private readonly int length;

            public BitmapMemoryManager(Bitmap bitmap)
            {
                if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    throw new ArgumentException("bitmap.PixelFormat != PixelFormat.Format32bppArgb", nameof(bitmap));
                }

                this.bitmap = bitmap;
                System.Drawing.Rectangle rectangle = new(0, 0, bitmap.Width, bitmap.Height);
                this.bmpData = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, bitmap.PixelFormat);
                this.length = bitmap.Width * bitmap.Height;
            }

            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                if (this.IsDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    this.bitmap.UnlockBits(this.bmpData);
                }

                this.IsDisposed = true;
            }

            public override unsafe Span<Bgra32> GetSpan()
            {
                void* ptr = (void*)this.bmpData.Scan0;
                return new Span<Bgra32>(ptr, this.length);
            }

            public override unsafe MemoryHandle Pin(int elementIndex = 0)
            {
                void* ptr = (void*)this.bmpData.Scan0;
                return new MemoryHandle(ptr, pinnable: this);
            }

            public override void Unpin()
            {
            }
        }

        public sealed class CastMemoryManager<TFrom, TTo> : MemoryManager<TTo>
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            private readonly Memory<TFrom> memory;

            public CastMemoryManager(Memory<TFrom> memory)
            {
                this.memory = memory;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
            }

            /// <inheritdoc/>
            public override Span<TTo> GetSpan()
            {
                return MemoryMarshal.Cast<TFrom, TTo>(this.memory.Span);
            }

            /// <inheritdoc/>
            public override MemoryHandle Pin(int elementIndex = 0)
            {
                int byteOffset = elementIndex * Unsafe.SizeOf<TTo>();
                int shiftedOffset = Math.DivRem(byteOffset, Unsafe.SizeOf<TFrom>(), out int remainder);

                if (remainder != 0)
                {
                    ThrowHelper.ThrowArgumentException("The input index doesn't result in an aligned item access",
                        nameof(elementIndex));
                }

                return this.memory.Slice(shiftedOffset).Pin();
            }

            /// <inheritdoc/>
            public override void Unpin()
            {
            }
        }

        [Fact]
        public void WrapMemory_CreatedImageIsCorrect()
        {
            Configuration cfg = Configuration.CreateDefaultInstance();
            ImageMetadata metaData = new();

            Rgba32[] array = new Rgba32[25];
            Memory<Rgba32> memory = new(array);

            using (Image<Rgba32> image = Image.WrapMemory(cfg, memory, 5, 5, metaData))
            {
                Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMem));
                ref Rgba32 pixel0 = ref imageMem.Span[0];
                Assert.True(Unsafe.AreSame(ref array[0], ref pixel0));

                Assert.Equal(cfg, image.Configuration);
                Assert.Equal(metaData, image.Metadata);
            }
        }

        [Fact]
        public void WrapSystemDrawingBitmap_WhenObserved()
        {
            if (ShouldSkipBitmapTest)
            {
                return;
            }

            using (Bitmap bmp = new(51, 23))
            {
                using (BitmapMemoryManager memoryManager = new(bmp))
                {
                    Memory<Bgra32> memory = memoryManager.Memory;
                    Bgra32 bg = Color.Red.ToPixel<Bgra32>();
                    Bgra32 fg = Color.Green.ToPixel<Bgra32>();

                    using (Image<Bgra32> image = Image.WrapMemory(memory, bmp.Width, bmp.Height))
                    {
                        Assert.Equal(memory, image.GetRootFramePixelBuffer().DangerousGetSingleMemory());
                        image.GetPixelMemoryGroup().Fill(bg);

                        image.ProcessPixelRows(accessor =>
                        {
                            for (int i = 10; i < 20; i++)
                            {
                                accessor.GetRowSpan(i).Slice(10, 10).Fill(fg);
                            }
                        });
                    }

                    Assert.False(memoryManager.IsDisposed);
                }

                if (!Directory.Exists(TestEnvironment.ActualOutputDirectoryFullPath))
                {
                    Directory.CreateDirectory(TestEnvironment.ActualOutputDirectoryFullPath);
                }

                string fn = System.IO.Path.Combine(
                    TestEnvironment.ActualOutputDirectoryFullPath,
                    $"{nameof(this.WrapSystemDrawingBitmap_WhenObserved)}.bmp");

                bmp.Save(fn, ImageFormat.Bmp);
            }
        }

        [Fact]
        public void WrapSystemDrawingBitmap_WhenOwned()
        {
            if (ShouldSkipBitmapTest)
            {
                return;
            }

            using (Bitmap bmp = new(51, 23))
            {
                BitmapMemoryManager memoryManager = new(bmp);
                Bgra32 bg = Color.Red.ToPixel<Bgra32>();
                Bgra32 fg = Color.Green.ToPixel<Bgra32>();

                using (Image<Bgra32> image = Image.WrapMemory(memoryManager, bmp.Width, bmp.Height))
                {
                    Assert.Equal(memoryManager.Memory, image.GetRootFramePixelBuffer().DangerousGetSingleMemory());
                    image.GetPixelMemoryGroup().Fill(bg);
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int i = 10; i < 20; i++)
                        {
                            accessor.GetRowSpan(i).Slice(10, 10).Fill(fg);
                        }
                    });
                }

                Assert.True(memoryManager.IsDisposed);

                string fn = System.IO.Path.Combine(
                    TestEnvironment.ActualOutputDirectoryFullPath,
                    $"{nameof(this.WrapSystemDrawingBitmap_WhenOwned)}.bmp");

                bmp.Save(fn, ImageFormat.Bmp);
            }
        }

        [Fact]
        public void WrapMemory_FromBytes_CreatedImageIsCorrect()
        {
            Configuration cfg = Configuration.CreateDefaultInstance();
            ImageMetadata metaData = new();

            byte[] array = new byte[25 * Unsafe.SizeOf<Rgba32>()];
            Memory<byte> memory = new(array);

            using (Image<Rgba32> image = Image.WrapMemory<Rgba32>(cfg, memory, 5, 5, metaData))
            {
                Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMem));
                ref Rgba32 pixel0 = ref imageMem.Span[0];
                Assert.True(Unsafe.AreSame(ref Unsafe.As<byte, Rgba32>(ref array[0]), ref pixel0));

                Assert.Equal(cfg, image.Configuration);
                Assert.Equal(metaData, image.Metadata);
            }
        }

        [Fact]
        public void WrapSystemDrawingBitmap_FromBytes_WhenObserved()
        {
            if (ShouldSkipBitmapTest)
            {
                return;
            }

            using (Bitmap bmp = new(51, 23))
            {
                using (BitmapMemoryManager memoryManager = new(bmp))
                {
                    Memory<Bgra32> pixelMemory = memoryManager.Memory;
                    Memory<byte> byteMemory = new CastMemoryManager<Bgra32, byte>(pixelMemory).Memory;
                    Bgra32 bg = Color.Red.ToPixel<Bgra32>();
                    Bgra32 fg = Color.Green.ToPixel<Bgra32>();

                    using (Image<Bgra32> image = Image.WrapMemory<Bgra32>(byteMemory, bmp.Width, bmp.Height))
                    {
                        Span<Bgra32> pixelSpan = pixelMemory.Span;
                        Span<Bgra32> imageSpan = image.GetRootFramePixelBuffer().DangerousGetSingleMemory().Span;

                        // We can't compare the two Memory<T> instances directly as they wrap different memory managers.
                        // To check that the underlying data matches, we can just manually check their lenth, and the
                        // fact that a reference to the first pixel in both spans is actually the same memory location.
                        Assert.Equal(pixelSpan.Length, imageSpan.Length);
                        Assert.True(Unsafe.AreSame(ref pixelSpan.GetPinnableReference(),
                            ref imageSpan.GetPinnableReference()));

                        image.GetPixelMemoryGroup().Fill(bg);
                        image.ProcessPixelRows(accessor =>
                        {
                            for (int i = 10; i < 20; i++)
                            {
                                accessor.GetRowSpan(i).Slice(10, 10).Fill(fg);
                            }
                        });
                    }

                    Assert.False(memoryManager.IsDisposed);
                }

                string fn = System.IO.Path.Combine(
                    TestEnvironment.ActualOutputDirectoryFullPath,
                    $"{nameof(this.WrapSystemDrawingBitmap_WhenObserved)}.bmp");

                bmp.Save(fn, ImageFormat.Bmp);
            }
        }

        [Theory]
        [InlineData(20, 5, 5)]
        [InlineData(1023, 32, 32)]
        [InlineData(65536, 65537, 65536)]
        public unsafe void WrapMemory_Throws_OnTooLessWrongSize(int size, int width, int height)
        {
            Configuration cfg = Configuration.CreateDefaultInstance();
            ImageMetadata metaData = new();

            Rgba32[] array = new Rgba32[size];
            Exception thrownException = null;
            fixed (void* ptr = array)
            {
                try
                {
                    using Image<Rgba32> image = Image.WrapMemory<Rgba32>(cfg, ptr, size * sizeof(Rgba32), width, height, metaData);
                }
                catch (Exception e)
                {
                    thrownException = e;
                }
            }

            Assert.IsType<ArgumentOutOfRangeException>(thrownException);
        }

        [Theory]
        [InlineData(25, 5, 5)]
        [InlineData(26, 5, 5)]
        [InlineData(2, 1, 1)]
        [InlineData(1024, 32, 32)]
        [InlineData(2048, 32, 32)]
        public unsafe void WrapMemory_FromPointer_CreatedImageIsCorrect(int size, int width, int height)
        {
            Configuration cfg = Configuration.CreateDefaultInstance();
            ImageMetadata metaData = new();

            Rgba32[] array = new Rgba32[size];

            fixed (void* ptr = array)
            {
                using (Image<Rgba32> image = Image.WrapMemory<Rgba32>(cfg, ptr, size * sizeof(Rgba32), width, height, metaData))
                {
                    Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMem));
                    Span<Rgba32> imageSpan = imageMem.Span;
                    Span<Rgba32> sourceSpan = array.AsSpan(0, width * height);
                    ref Rgba32 pixel0 = ref imageSpan[0];
                    Assert.True(Unsafe.AreSame(ref sourceSpan[0], ref pixel0));
                    ref Rgba32 pixel_1 = ref imageSpan[imageSpan.Length - 1];
                    Assert.True(Unsafe.AreSame(ref sourceSpan[sourceSpan.Length - 1], ref pixel_1));

                    Assert.Equal(cfg, image.Configuration);
                    Assert.Equal(metaData, image.Metadata);
                }
            }
        }

        [Fact]
        public unsafe void WrapSystemDrawingBitmap_FromPointer()
        {
            if (ShouldSkipBitmapTest)
            {
                return;
            }

            using (Bitmap bmp = new(51, 23))
            {
                using (BitmapMemoryManager memoryManager = new(bmp))
                {
                    Memory<Bgra32> pixelMemory = memoryManager.Memory;
                    Bgra32 bg = Color.Red.ToPixel<Bgra32>();
                    Bgra32 fg = Color.Green.ToPixel<Bgra32>();

                    fixed (void* p = pixelMemory.Span)
                    {
                        using (Image<Bgra32> image = Image.WrapMemory<Bgra32>(p, pixelMemory.Length, bmp.Width, bmp.Height))
                        {
                            Span<Bgra32> pixelSpan = pixelMemory.Span;
                            Span<Bgra32> imageSpan = image.GetRootFramePixelBuffer().DangerousGetSingleMemory().Span;

                            Assert.Equal(pixelSpan.Length, imageSpan.Length);
                            Assert.True(Unsafe.AreSame(ref pixelSpan.GetPinnableReference(),
                                ref imageSpan.GetPinnableReference()));

                            image.GetPixelMemoryGroup().Fill(bg);
                            image.ProcessPixelRows(accessor =>
                            {
                                for (int i = 10; i < 20; i++)
                                {
                                    accessor.GetRowSpan(i).Slice(10, 10).Fill(fg);
                                }
                            });
                        }

                        Assert.False(memoryManager.IsDisposed);
                    }
                }

                string fn = System.IO.Path.Combine(
                    TestEnvironment.ActualOutputDirectoryFullPath,
                    $"{nameof(this.WrapSystemDrawingBitmap_WhenObserved)}.bmp");

                bmp.Save(fn, ImageFormat.Bmp);
            }
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(20, 5, 5)]
        [InlineData(1023, 32, 32)]
        [InlineData(65536, 65537, 65536)]
        public void WrapMemory_MemoryOfT_InvalidSize(int size, int height, int width)
        {
            Rgba32[] array = new Rgba32[size];
            Memory<Rgba32> memory = new(array);

            Assert.Throws<ArgumentException>(() => Image.WrapMemory(memory, height, width));
        }

        [Theory]
        [InlineData(25, 5, 5)]
        [InlineData(26, 5, 5)]
        [InlineData(2, 1, 1)]
        [InlineData(1024, 32, 32)]
        [InlineData(2048, 32, 32)]
        public void WrapMemory_MemoryOfT_ValidSize(int size, int height, int width)
        {
            Rgba32[] array = new Rgba32[size];
            Memory<Rgba32> memory = new(array);

            Image.WrapMemory(memory, height, width);
        }

        private class TestMemoryOwner<T> : IMemoryOwner<T>
        {
            public bool Disposed { get; private set; }

            public Memory<T> Memory { get; set; }

            public void Dispose() => this.Disposed = true;
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(20, 5, 5)]
        [InlineData(1023, 32, 32)]
        [InlineData(65536, 65537, 65536)]
        public void WrapMemory_IMemoryOwnerOfT_InvalidSize(int size, int height, int width)
        {
            Rgba32[] array = new Rgba32[size];
            TestMemoryOwner<Rgba32> memory = new() { Memory = array };

            Assert.Throws<ArgumentException>(() => Image.WrapMemory(memory, height, width));
        }

        [Theory]
        [InlineData(25, 5, 5)]
        [InlineData(26, 5, 5)]
        [InlineData(2, 1, 1)]
        [InlineData(1024, 32, 32)]
        [InlineData(2048, 32, 32)]
        public void WrapMemory_IMemoryOwnerOfT_ValidSize(int size, int height, int width)
        {
            Rgba32[] array = new Rgba32[size];
            TestMemoryOwner<Rgba32> memory = new() { Memory = array };

            using (Image<Rgba32> img = Image.WrapMemory<Rgba32>(memory, width, height))
            {
                Assert.Equal(width, img.Width);
                Assert.Equal(height, img.Height);

                img.ProcessPixelRows(accessor =>
                {
                    for (int i = 0; i < height; ++i)
                    {
                        int arrayIndex = width * i;

                        Span<Rgba32> rowSpan = accessor.GetRowSpan(i);
                        ref Rgba32 r0 = ref rowSpan[0];
                        ref Rgba32 r1 = ref array[arrayIndex];

                        Assert.True(Unsafe.AreSame(ref r0, ref r1));
                    }
                });
            }

            Assert.True(memory.Disposed);
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(20, 5, 5)]
        [InlineData(1023, 32, 32)]
        [InlineData(65536, 65537, 65536)]
        public void WrapMemory_IMemoryOwnerOfByte_InvalidSize(int size, int height, int width)
        {
            byte[] array = new byte[size * Unsafe.SizeOf<Rgba32>()];
            TestMemoryOwner<byte> memory = new() { Memory = array };

            Assert.Throws<ArgumentException>(() => Image.WrapMemory<Rgba32>(memory, height, width));
        }

        [Theory]
        [InlineData(25, 5, 5)]
        [InlineData(26, 5, 5)]
        [InlineData(2, 1, 1)]
        [InlineData(1024, 32, 32)]
        [InlineData(2048, 32, 32)]
        public void WrapMemory_IMemoryOwnerOfByte_ValidSize(int size, int height, int width)
        {
            int pixelSize = Unsafe.SizeOf<Rgba32>();
            byte[] array = new byte[size * pixelSize];
            TestMemoryOwner<byte> memory = new() { Memory = array };

            using (Image<Rgba32> img = Image.WrapMemory<Rgba32>(memory, width, height))
            {
                Assert.Equal(width, img.Width);
                Assert.Equal(height, img.Height);

                img.ProcessPixelRows(acccessor =>
                {
                    for (int i = 0; i < height; ++i)
                    {
                        int arrayIndex = pixelSize * width * i;

                        Span<Rgba32> rowSpan = acccessor.GetRowSpan(i);
                        ref Rgba32 r0 = ref rowSpan[0];
                        ref Rgba32 r1 = ref Unsafe.As<byte, Rgba32>(ref array[arrayIndex]);

                        Assert.True(Unsafe.AreSame(ref r0, ref r1));
                    }
                });
            }

            Assert.True(memory.Disposed);
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(20, 5, 5)]
        [InlineData(1023, 32, 32)]
        [InlineData(65536, 65537, 65536)]
        public void WrapMemory_MemoryOfByte_InvalidSize(int size, int height, int width)
        {
            byte[] array = new byte[size * Unsafe.SizeOf<Rgba32>()];
            Memory<byte> memory = new(array);

            Assert.Throws<ArgumentException>(() => Image.WrapMemory<Rgba32>(memory, height, width));
        }

        [Theory]
        [InlineData(25, 5, 5)]
        [InlineData(26, 5, 5)]
        [InlineData(2, 1, 1)]
        [InlineData(1024, 32, 32)]
        [InlineData(2048, 32, 32)]
        public void WrapMemory_MemoryOfByte_ValidSize(int size, int height, int width)
        {
            byte[] array = new byte[size * Unsafe.SizeOf<Rgba32>()];
            Memory<byte> memory = new(array);

            Image.WrapMemory<Rgba32>(memory, height, width);
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(20, 5, 5)]
        [InlineData(26, 5, 5)]
        [InlineData(2, 1, 1)]
        [InlineData(1023, 32, 32)]
        public unsafe void WrapMemory_Pointer_Null(int size, int height, int width)
        {
            Assert.Throws<ArgumentException>(() => Image.WrapMemory<Rgba32>((void*)null, size, height, width));
        }

        private static bool ShouldSkipBitmapTest =>
            !TestEnvironment.Is64BitProcess || (TestHelpers.ImageSharpBuiltAgainst != "netcoreapp3.1" &&
                                                TestHelpers.ImageSharpBuiltAgainst != "netcoreapp2.1");
    }
}
