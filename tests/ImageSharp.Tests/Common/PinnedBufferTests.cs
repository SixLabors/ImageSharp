namespace ImageSharp.Tests.Common
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Xunit;

    using static TestStructs;

    public unsafe class PinnedBufferTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData(1111)]
        public void ConstructWithOwnArray(int count)
        {
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(count))
            {
                Assert.False(buffer.IsDisposedOrLostArrayOwnership);
                Assert.NotNull(buffer.Array);
                Assert.Equal(count, buffer.Length);
                Assert.True(buffer.Array.Length >= count);

                VerifyPointer(buffer);
            }
        }
        
        [Theory]
        [InlineData(42)]
        [InlineData(1111)]
        public void ConstructWithExistingArray(int count)
        {
            Foo[] array = new Foo[count];
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(array))
            {
                Assert.False(buffer.IsDisposedOrLostArrayOwnership);
                Assert.Equal(array, buffer.Array);
                Assert.Equal(count, buffer.Length);

                VerifyPointer(buffer);
            }
        }

        [Theory]
        [InlineData(42)]
        [InlineData(1111)]
        public void Clear(int count)
        {
            Foo[] a = { new Foo() { A = 1, B = 2 }, new Foo() { A = 3, B = 4 } };
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(a))
            {
                buffer.Clear();

                Assert.Equal(default(Foo), a[0]);
                Assert.Equal(default(Foo), a[1]);
            }
        }

        [Fact]
        public void CreateClean()
        {
            for (int i = 0; i < 100; i++)
            {
                using (PinnedBuffer<int> buffer = PinnedBuffer<int>.CreateClean(42))
                {
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        Assert.Equal(0, buffer.Array[j]);
                        buffer.Array[j] = 666;
                    }
                }
            }
        }

        public class Indexer
        {
            public static readonly TheoryData<int, int> IndexerData =
                new TheoryData<int,int>()
                    {
                        { 10, 0 },
                        { 16, 3 },
                        { 10, 9 }
                    };

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Read(int length, int index)
            {
                Foo[] a = Foo.CreateArray(length);
                
                using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(a))
                {
                    Foo element = buffer[index];

                    Assert.Equal(a[index], element);
                }
            }

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Write(int length, int index)
            {
                Foo[] a = Foo.CreateArray(length);

                using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(a))
                {
                    buffer[index] = new Foo(666, 666);

                    Assert.Equal(new Foo(666, 666), a[index]);
                }
            }
        }

        [Fact]
        public void Dispose()
        {
            PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(42);
            buffer.Dispose();

            Assert.True(buffer.IsDisposedOrLostArrayOwnership);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(123)]
        public void CastToSpan(int bufferLength)
        {
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(bufferLength))
            {
                BufferSpan<Foo> span = buffer;

                Assert.Equal(buffer.Array, span.Array);
                Assert.Equal(0, span.Start);
                Assert.Equal(buffer.Pointer, span.PointerAtOffset);
                Assert.Equal(span.Length, bufferLength);
            }
        }

        [Fact]
        public void Span()
        {
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(42))
            {
                BufferSpan<Foo> span = buffer.Span;

                Assert.Equal(buffer.Array, span.Array);
                Assert.Equal(0, span.Start);
                Assert.Equal(buffer.Pointer, span.PointerAtOffset);
                Assert.Equal(span.Length, 42);
            }
        }

        public class Slice
        {

            [Theory]
            [InlineData(7, 2)]
            [InlineData(123, 17)]
            public void WithStartOnly(int bufferLength, int start)
            {
                using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(bufferLength))
                {
                    BufferSpan<Foo> span = buffer.Slice(start);

                    Assert.Equal(buffer.Array, span.Array);
                    Assert.Equal(start, span.Start);
                    Assert.Equal(buffer.Pointer + start * Unsafe.SizeOf<Foo>(), span.PointerAtOffset);
                    Assert.Equal(span.Length, bufferLength - start);
                }
            }

            [Theory]
            [InlineData(7, 2, 5)]
            [InlineData(123, 17, 42)]
            public void WithStartAndLength(int bufferLength, int start, int spanLength)
            {
                using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(bufferLength))
                {
                    BufferSpan<Foo> span = buffer.Slice(start, spanLength);

                    Assert.Equal(buffer.Array, span.Array);
                    Assert.Equal(start, span.Start);
                    Assert.Equal(buffer.Pointer + start * Unsafe.SizeOf<Foo>(), span.PointerAtOffset);
                    Assert.Equal(span.Length, spanLength);
                }
            }
        }
        
        [Fact]
        public void UnPinAndTakeArrayOwnership()
        {
            Foo[] data = null;
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(42))
            {
                data = buffer.UnPinAndTakeArrayOwnership();
                Assert.True(buffer.IsDisposedOrLostArrayOwnership);
            }

            Assert.NotNull(data);
            Assert.True(data.Length >= 42);
        }

        private static void VerifyPointer(PinnedBuffer<Foo> buffer)
        {
            IntPtr ptr = (IntPtr)Unsafe.AsPointer(ref buffer.Array[0]);
            Assert.Equal(ptr, buffer.Pointer);
        }
    }
}