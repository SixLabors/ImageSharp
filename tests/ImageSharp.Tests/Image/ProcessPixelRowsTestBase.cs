// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
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
            using var image = new Image<Rgb24>(123, 456);
            this.ProcessPixelRowsImpl(image, accessor =>
            {
                Assert.Equal(123, accessor.Width);
                Assert.Equal(456, accessor.Height);
            });
        }

        [Fact]
        public void WriteImagePixels_SingleImage()
        {
            using var image = new Image<L16>(256, 256);
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
            using var img1 = new Image<L16>(256, 256);
            Buffer2D<L16> buffer = img1.Frames.RootFrame.PixelBuffer;
            for (int y = 0; y < 256; y++)
            {
                Span<L16> row = buffer.DangerousGetRowSpan(y);
                for (int x = 0; x < 256; x++)
                {
                    row[x] = new L16((ushort)(x * y));
                }
            }

            using var img2 = new Image<L16>(256, 256);

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
            using var img1 = new Image<L16>(256, 256);
            Buffer2D<L16> buffer2 = img1.Frames.RootFrame.PixelBuffer;
            for (int y = 0; y < 256; y++)
            {
                Span<L16> row = buffer2.DangerousGetRowSpan(y);
                for (int x = 0; x < 256; x++)
                {
                    row[x] = new L16((ushort)(x * y));
                }
            }

            using var img2 = new Image<L16>(256, 256);
            using var img3 = new Image<L16>(256, 256);

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
            using var nonDisposed = new Image<L16>(1, 1);
            var disposed = new Image<L16>(1, 1);
            disposed.Dispose();

            Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(disposed, _ => { }));

            Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(disposed, nonDisposed, (_, _) => { }));
            Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(nonDisposed, disposed, (_, _) => { }));

            Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(disposed, nonDisposed, nonDisposed, (_, _, _) => { }));
            Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(nonDisposed, disposed, nonDisposed, (_, _, _) => { }));
            Assert.Throws<ObjectDisposedException>(() => this.ProcessPixelRowsImpl(nonDisposed, nonDisposed, disposed, (_, _, _) => { }));
        }

        [Fact]
        public void RetainsUnmangedBuffers1()
        {
            RemoteExecutor.Invoke(RunTest, this.GetType().FullName).Dispose();

            static void RunTest(string testTypeName)
            {
                var buffer = new UnmanagedBuffer<L8>(100);
                var allocator = new MockUnmanagedMemoryAllocator<L8>(buffer);
                Configuration.Default.MemoryAllocator = allocator;

                var image = new Image<L8>(10, 10);

                Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                GetTest(testTypeName).ProcessPixelRowsImpl(image, _ =>
                {
                    buffer.BufferHandle.Dispose();
                    Assert.Equal(1, UnmanagedMemoryHandle.TotalOutstandingHandles);
                });
                Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }
        }

        [Fact]
        public void RetainsUnmangedBuffers2()
        {
            RemoteExecutor.Invoke(RunTest, this.GetType().FullName).Dispose();

            static void RunTest(string testTypeName)
            {
                var buffer1 = new UnmanagedBuffer<L8>(100);
                var buffer2 = new UnmanagedBuffer<L8>(100);
                var allocator = new MockUnmanagedMemoryAllocator<L8>(buffer1, buffer2);
                Configuration.Default.MemoryAllocator = allocator;

                var image1 = new Image<L8>(10, 10);
                var image2 = new Image<L8>(10, 10);

                Assert.Equal(2, UnmanagedMemoryHandle.TotalOutstandingHandles);
                GetTest(testTypeName).ProcessPixelRowsImpl(image1, image2, (_, _) =>
                {
                    buffer1.BufferHandle.Dispose();
                    buffer2.BufferHandle.Dispose();
                    Assert.Equal(2, UnmanagedMemoryHandle.TotalOutstandingHandles);
                });
                Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }
        }

        [Fact]
        public void RetainsUnmangedBuffers3()
        {
            RemoteExecutor.Invoke(RunTest, this.GetType().FullName).Dispose();

            static void RunTest(string testTypeName)
            {
                var buffer1 = new UnmanagedBuffer<L8>(100);
                var buffer2 = new UnmanagedBuffer<L8>(100);
                var buffer3 = new UnmanagedBuffer<L8>(100);
                var allocator = new MockUnmanagedMemoryAllocator<L8>(buffer1, buffer2, buffer3);
                Configuration.Default.MemoryAllocator = allocator;

                var image1 = new Image<L8>(10, 10);
                var image2 = new Image<L8>(10, 10);
                var image3 = new Image<L8>(10, 10);

                Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
                GetTest(testTypeName).ProcessPixelRowsImpl(image1, image2, image3, (_, _, _) =>
                {
                    buffer1.BufferHandle.Dispose();
                    buffer2.BufferHandle.Dispose();
                    buffer3.BufferHandle.Dispose();
                    Assert.Equal(3, UnmanagedMemoryHandle.TotalOutstandingHandles);
                });
                Assert.Equal(0, UnmanagedMemoryHandle.TotalOutstandingHandles);
            }
        }

        private static ProcessPixelRowsTestBase GetTest(string testTypeName)
        {
            Type type = typeof(ProcessPixelRowsTestBase).Assembly.GetType(testTypeName);
            return (ProcessPixelRowsTestBase)Activator.CreateInstance(type);
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
                (IMemoryOwner<T>)this.buffers.Pop();
        }
    }
}
