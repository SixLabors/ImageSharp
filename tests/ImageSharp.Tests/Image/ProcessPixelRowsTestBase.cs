// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public abstract class ProcessPixelRowsTestBase
{
    protected abstract void ProcessPixelRowsImpl<TPixel>(
        Image<TPixel> image,
        PixelAccessorAction<TPixel> processPixels)
        where TPixel : unmanaged, IPixel<TPixel>;

    protected abstract void ProcessPixelRowsImpl<TPixel>(
        Image<TPixel> image1,
        Image<TPixel> image2,
        PixelAccessorAction<TPixel, TPixel> processPixels)
        where TPixel : unmanaged, IPixel<TPixel>;

    protected abstract void ProcessPixelRowsImpl<TPixel>(
        Image<TPixel> image1,
        Image<TPixel> image2,
        Image<TPixel> image3,
        PixelAccessorAction<TPixel, TPixel, TPixel> processPixels)
        where TPixel : unmanaged, IPixel<TPixel>;

    [Fact]
    public void PixelAccessorDimensionsAreCorrect()
    {
        using Image<Rgb24> image = new Image<Rgb24>(123, 456);
        this.ProcessPixelRowsImpl(image, accessor =>
        {
            Assert.Equal(123, accessor.Width);
            Assert.Equal(456, accessor.Height);
        });
    }

    [Fact]
    public void WriteImagePixels_SingleImage()
    {
        using Image<L16> image = new Image<L16>(256, 256);
        this.ProcessPixelRowsImpl(image, accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<L16> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new L16((ushort)(x * y));
                }
            }
        });

        Buffer2D<L16> buffer = image.Frames.RootFrame.PixelBuffer;
        for (int y = 0; y < 256; y++)
        {
            Span<L16> row = buffer.DangerousGetRowSpan(y);
            for (int x = 0; x < 256; x++)
            {
                int actual = row[x].PackedValue;
                Assert.Equal(x * y, actual);
            }
        }
    }

    [Fact]
    public void WriteImagePixels_MultiImage2()
    {
        using Image<L16> img1 = new Image<L16>(256, 256);
        Buffer2D<L16> buffer = img1.Frames.RootFrame.PixelBuffer;
        for (int y = 0; y < 256; y++)
        {
            Span<L16> row = buffer.DangerousGetRowSpan(y);
            for (int x = 0; x < 256; x++)
            {
                row[x] = new L16((ushort)(x * y));
            }
        }

        using Image<L16> img2 = new Image<L16>(256, 256);

        this.ProcessPixelRowsImpl(img1, img2, (accessor1, accessor2) =>
        {
            for (int y = 0; y < accessor1.Height; y++)
            {
                Span<L16> row1 = accessor1.GetRowSpan(y);
                Span<L16> row2 = accessor2.GetRowSpan(accessor2.Height - y - 1);
                row1.CopyTo(row2);
            }
        });

        buffer = img2.Frames.RootFrame.PixelBuffer;
        for (int y = 0; y < 256; y++)
        {
            Span<L16> row = buffer.DangerousGetRowSpan(y);
            for (int x = 0; x < 256; x++)
            {
                int actual = row[x].PackedValue;
                Assert.Equal(x * (256 - y - 1), actual);
            }
        }
    }

    [Fact]
    public void WriteImagePixels_MultiImage3()
    {
        using Image<L16> img1 = new Image<L16>(256, 256);
        Buffer2D<L16> buffer2 = img1.Frames.RootFrame.PixelBuffer;
        for (int y = 0; y < 256; y++)
        {
            Span<L16> row = buffer2.DangerousGetRowSpan(y);
            for (int x = 0; x < 256; x++)
            {
                row[x] = new L16((ushort)(x * y));
            }
        }

        using Image<L16> img2 = new Image<L16>(256, 256);
        using Image<L16> img3 = new Image<L16>(256, 256);

        this.ProcessPixelRowsImpl(img1, img2, img3, (accessor1, accessor2, accessor3) =>
        {
            for (int y = 0; y < accessor1.Height; y++)
            {
                Span<L16> row1 = accessor1.GetRowSpan(y);
                Span<L16> row2 = accessor2.GetRowSpan(accessor2.Height - y - 1);
                Span<L16> row3 = accessor3.GetRowSpan(y);
                row1.CopyTo(row2);
                row1.CopyTo(row3);
            }
        });

        buffer2 = img2.Frames.RootFrame.PixelBuffer;
        Buffer2D<L16> buffer3 = img3.Frames.RootFrame.PixelBuffer;
        for (int y = 0; y < 256; y++)
        {
            Span<L16> row2 = buffer2.DangerousGetRowSpan(y);
            Span<L16> row3 = buffer3.DangerousGetRowSpan(y);
            for (int x = 0; x < 256; x++)
            {
                int actual2 = row2[x].PackedValue;
                int actual3 = row3[x].PackedValue;
                Assert.Equal(x * (256 - y - 1), actual2);
                Assert.Equal(x * y, actual3);
            }
        }
    }

    [Fact]
    public void Disposed_ThrowsObjectDisposedException()
    {
        using Image<L16> nonDisposed = new Image<L16>(1, 1);
        Image<L16> disposed = new Image<L16>(1, 1);
        disposed.Dispose();

        Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(disposed, _ => { }));

        Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(disposed, nonDisposed, (_, _) => { }));
        Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(nonDisposed, disposed, (_, _) => { }));

        Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(disposed, nonDisposed, nonDisposed, (_, _, _) => { }));
        Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(nonDisposed, disposed, nonDisposed, (_, _, _) => { }));
        Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(nonDisposed, nonDisposed, disposed, (_, _, _) => { }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainsUnmangedBuffers1(bool throwException)
    {
        RemoteExecutor.Invoke(RunTest, this.GetType().FullName, throwException.ToString()).Dispose();

        static void RunTest(string testTypeName, string throwExceptionStr)
        {
            bool throwExceptionInner = bool.Parse(throwExceptionStr);
            UnmanagedBuffer<L8> buffer = UnmanagedBuffer<L8>.Allocate(100);
            MockUnmanagedMemoryAllocator<L8> allocator = new MockUnmanagedMemoryAllocator<L8>(buffer);
            Configuration.Default.MemoryAllocator = allocator;

            Image<L8> image = new Image<L8>(10, 10);

            Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
            try
            {
                GetTest(testTypeName).ProcessPixelRowsImpl(image, _ =>
                {
                    ((IDisposable)buffer).Dispose();
                    Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    if (throwExceptionInner)
                    {
                        throw new NonFatalException();
                    }
                });
            }
            catch (NonFatalException)
            {
            }

            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainsUnmangedBuffers2(bool throwException)
    {
        RemoteExecutor.Invoke(RunTest, this.GetType().FullName, throwException.ToString()).Dispose();

        static void RunTest(string testTypeName, string throwExceptionStr)
        {
            bool throwExceptionInner = bool.Parse(throwExceptionStr);
            UnmanagedBuffer<L8> buffer1 = UnmanagedBuffer<L8>.Allocate(100);
            UnmanagedBuffer<L8> buffer2 = UnmanagedBuffer<L8>.Allocate(100);
            MockUnmanagedMemoryAllocator<L8> allocator = new MockUnmanagedMemoryAllocator<L8>(buffer1, buffer2);
            Configuration.Default.MemoryAllocator = allocator;

            Image<L8> image1 = new Image<L8>(10, 10);
            Image<L8> image2 = new Image<L8>(10, 10);

            Assert.Equal(2, UnmanagedMemoryHandle.TotalOutstandingHandles);
            try
            {
                GetTest(testTypeName).ProcessPixelRowsImpl(image1, image2, (_, _) =>
                {
                    ((IDisposable)buffer1).Dispose();
                    ((IDisposable)buffer2).Dispose();
                    Assert.Equal(2, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    if (throwExceptionInner)
                    {
                        throw new NonFatalException();
                    }
                });
            }
            catch (NonFatalException)
            {
            }

            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainsUnmangedBuffers3(bool throwException)
    {
        RemoteExecutor.Invoke(RunTest, this.GetType().FullName, throwException.ToString()).Dispose();

        static void RunTest(string testTypeName, string throwExceptionStr)
        {
            bool throwExceptionInner = bool.Parse(throwExceptionStr);
            UnmanagedBuffer<L8> buffer1 = UnmanagedBuffer<L8>.Allocate(100);
            UnmanagedBuffer<L8> buffer2 = UnmanagedBuffer<L8>.Allocate(100);
            UnmanagedBuffer<L8> buffer3 = UnmanagedBuffer<L8>.Allocate(100);
            MockUnmanagedMemoryAllocator<L8> allocator = new MockUnmanagedMemoryAllocator<L8>(buffer1, buffer2, buffer3);
            Configuration.Default.MemoryAllocator = allocator;

            Image<L8> image1 = new Image<L8>(10, 10);
            Image<L8> image2 = new Image<L8>(10, 10);
            Image<L8> image3 = new Image<L8>(10, 10);

            Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
            try
            {
                GetTest(testTypeName).ProcessPixelRowsImpl(image1, image2, image3, (_, _, _) =>
                {
                    ((IDisposable)buffer1).Dispose();
                    ((IDisposable)buffer2).Dispose();
                    ((IDisposable)buffer3).Dispose();
                    Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
                    if (throwExceptionInner)
                    {
                        throw new NonFatalException();
                    }
                });
            }
            catch (NonFatalException)
            {
            }

            Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
        }
    }

    private static ProcessPixelRowsTestBase GetTest(string testTypeName)
    {
        Type type = typeof(ProcessPixelRowsTestBase).Assembly.GetType(testTypeName);
        return (ProcessPixelRowsTestBase)Activator.CreateInstance(type);
    }

    private class NonFatalException : Exception
    {
    }

    private class MockUnmanagedMemoryAllocator<T1> : MemoryAllocator
        where T1 : struct
    {
        private Stack<UnmanagedBuffer<T1>> buffers = new();

        public MockUnmanagedMemoryAllocator(params UnmanagedBuffer<T1>[] buffers)
        {
            foreach (UnmanagedBuffer<T1> buffer in buffers)
            {
                this.buffers.Push(buffer);
            }
        }

        protected internal override int GetBufferCapacityInBytes() => int.MaxValue;

        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None) =>
            this.buffers.Pop() as IMemoryOwner<T>;
    }
}
