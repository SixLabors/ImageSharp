using System;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Memory
{
    public class MmfSandbox
    {
        private readonly MmfAllocator allocator = new MmfAllocator();

        private readonly ITestOutputHelper output;

        public MmfSandbox(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public unsafe void Hello()
        {
            long capacity = 128 * 1024 * 1024;
            using (var mmf = MemoryMappedFile.CreateNew("hello", capacity, MemoryMappedFileAccess.ReadWrite))
            {
                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor())
                {
                    byte* ptr = default;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

                    Assert.True(ptr != default);

                    int* lol = (int*)ptr;
                    for (int i = 0; i < 1024; i++)
                    {
                        lol[i] = i;
                    }
                }

                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor())
                {
                    byte* ptr = default;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

                    int* lol = (int*)ptr;
                    for (int i = 0; i < 1024; i++)
                    {
                        Assert.Equal(i, lol[i]);
                    }
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        public void CreateMmfImage<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            var configuration =
                new Configuration(new PngConfigurationModule(), new JpegConfigurationModule())
                    {
                        MemoryAllocator = this.allocator
                    };

            byte[] encodedData = TestFile.Create(TestImages.Jpeg.Baseline.Jpeg420Exif).Bytes;

            using (var image = Image.Load<TPixel>(configuration, encodedData))
            {
                TPixel fg = NamedColors<TPixel>.RebeccaPurple;
                image.Mutate(c => c.Fill(fg, new RectangularPolygon(500, 500, 500, 500)));

                image.DebugSave(dummyProvider, new PngEncoder(), "png", false, false);
                image.DebugSave(dummyProvider, new JpegEncoder(), "jpg", false, false);
                image.DebugSave(dummyProvider, new BmpEncoder(), "bmp", false, false);
                image.DebugSave(dummyProvider, new GifEncoder(), "gif", false, false);
            }

            this.output.WriteLine("Allocator invocations: " + this.allocator.InvodationCounter);
            Assert.True(this.allocator.InvodationCounter > 10);
        }

        class MmfAllocator : MemoryAllocator
        {
            unsafe class MmfMemoryManager<T> : MemoryManager<T>
                where T : struct
            {
                private readonly MemoryMappedFile mmf;

                private readonly MemoryMappedViewAccessor view;

                private readonly byte* ptr;

                private readonly int length;

                public MmfMemoryManager(int length)
                {
                    this.length = length;
                    long capacityInBytes = length * Unsafe.SizeOf<T>();
                    this.mmf = MemoryMappedFile.CreateNew(Guid.NewGuid().ToString(), capacityInBytes);
                    this.view = this.mmf.CreateViewAccessor();
                    this.view.SafeMemoryMappedViewHandle.AcquirePointer(ref this.ptr);
                }

                protected override void Dispose(bool disposing)
                {
                    if (disposing)
                    {
                        this.view.SafeMemoryMappedViewHandle.ReleasePointer();
                        this.view.Dispose();
                        this.mmf.Dispose();
                    }
                }

                public override Span<T> GetSpan()
                {
                    return new Span<T>(this.ptr, this.length);
                }

                public override MemoryHandle Pin(int elementIndex = 0)
                {
                    return new MemoryHandle(this.ptr);
                }

                public override void Unpin()
                {
                }
            }

            public int InvodationCounter { get; private set; }

            internal override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
            {
                this.InvodationCounter++;
                return new MmfMemoryManager<T>(length);
            }

            internal override IManagedByteBuffer AllocateManagedByteBuffer(int length, AllocationOptions options = AllocationOptions.None)
            {
                return new BasicByteBuffer(new byte[length]);
            }
        }
    }
}