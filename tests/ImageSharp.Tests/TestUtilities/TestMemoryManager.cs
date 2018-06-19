using System;
using System.Buffers;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.PixelFormats;

    class TestMemoryManager<T> : MemoryManager<T>
        where T : struct, IPixel<T>
    {
        public TestMemoryManager(T[] pixelArray)
        {
            this.PixelArray = pixelArray;
        }

        public T[] PixelArray { get; }

        protected override void Dispose(bool disposing)
        {
        }

        public override Span<T> GetSpan()
        {
            return this.PixelArray;
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            throw new NotImplementedException();
        }

        public override void Unpin()
        {
            throw new NotImplementedException();
        }

        public static TestMemoryManager<T> CreateAsCopyOfPixelData(Span<T> pixelData)
        {
            var pixelArray = new T[pixelData.Length];
            pixelData.CopyTo(pixelArray);
            return new TestMemoryManager<T>(pixelArray);
        }

        public static TestMemoryManager<T> CreateAsCopyOfPixelData(Image<T> image)
        {
            return CreateAsCopyOfPixelData(image.GetPixelSpan());
        }
    }
}