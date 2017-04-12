// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Common
{
    using System;
    using System.Runtime.CompilerServices;

    using Xunit;

    using static TestStructs;

    public unsafe class PinnedImageBufferTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class Assert : Xunit.Assert
        {
            public static void SpanPointsTo<T>(IntPtr ptr, BufferSpan<T> span)
                where T : struct
            {
                ref byte r = ref Unsafe.As<T, byte>(ref span.DangerousGetPinnableReference());

                void* p = Unsafe.AsPointer(ref r);

                Assert.Equal(ptr, (IntPtr)p);
            }
        }

        [Theory]
        [InlineData(7, 42)]
        [InlineData(1025, 17)]
        public void Construct(int width, int height)
        {
            using (PinnedImageBuffer<Foo> buffer = new PinnedImageBuffer<Foo>(width, height))
            {
                Assert.Equal(width, buffer.Width);
                Assert.Equal(height, buffer.Height);
                Assert.Equal(width * height, buffer.Length);
            }
        }

        [Theory]
        [InlineData(7, 42)]
        [InlineData(1025, 17)]
        public void Construct_FromExternalArray(int width, int height)
        {
            Foo[] array = new Foo[width * height + 10];
            using (PinnedImageBuffer<Foo> buffer = new PinnedImageBuffer<Foo>(array, width, height))
            {
                Assert.Equal(width, buffer.Width);
                Assert.Equal(height, buffer.Height);
                Assert.Equal(width * height, buffer.Length);
            }
        }


        [Fact]
        public void CreateClean()
        {
            for (int i = 0; i < 100; i++)
            {
                using (PinnedImageBuffer<int> buffer = PinnedImageBuffer<int>.CreateClean(42, 42))
                {
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        Assert.Equal(0, buffer.Array[j]);
                        buffer.Array[j] = 666;
                    }
                }
            }
        }

        [Theory]
        [InlineData(7, 42, 0)]
        [InlineData(7, 42, 10)]
        [InlineData(17, 42, 41)]
        public void GetRowSpanY(int width, int height, int y)
        {
            using (PinnedImageBuffer<Foo> buffer = new PinnedImageBuffer<Foo>(width, height))
            {
                BufferSpan<Foo> span = buffer.GetRowSpan(y);

                Assert.Equal(width * y, span.Start);
                Assert.Equal(width, span.Length);
                Assert.SpanPointsTo(buffer.Pointer + sizeof(Foo) * width * y, span);
            }
        }

        [Theory]
        [InlineData(7, 42, 0, 0)]
        [InlineData(7, 42, 3, 10)]
        [InlineData(17, 42, 0, 41)]
        public void GetRowSpanXY(int width, int height, int x, int y)
        {
            using (PinnedImageBuffer<Foo> buffer = new PinnedImageBuffer<Foo>(width, height))
            {
                BufferSpan<Foo> span = buffer.GetRowSpan(x, y);

                Assert.Equal(width * y + x, span.Start);
                Assert.Equal(width - x, span.Length);
                Assert.SpanPointsTo(buffer.Pointer + sizeof(Foo) * (width * y + x), span);
            }
        }

        [Theory]
        [InlineData(42, 8, 0, 0)]
        [InlineData(400, 1000, 20, 10)]
        [InlineData(99, 88, 98, 87)]
        public void Indexer(int width, int height, int x, int y)
        {
            using (PinnedImageBuffer<Foo> buffer = new PinnedImageBuffer<Foo>(width, height))
            {
                Foo[] array = buffer.Array;

                ref Foo actual = ref buffer[x, y];

                ref Foo expected = ref array[y * width + x];

                Assert.True(Unsafe.AreSame(ref expected, ref actual));
            }
        }
    }
}