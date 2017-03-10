namespace ImageSharp.Tests.Common
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using Xunit;

    public unsafe class PinnedBufferTests
    {
        public struct Foo
        {
            public int A;

            public double B;
        }

        [Theory]
        [InlineData(42)]
        [InlineData(1111)]
        public void ConstructWithOwnArray(int count)
        {
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(count))
            {
                Assert.False(buffer.IsDisposedOrLostArrayOwnership);
                Assert.NotNull(buffer.Array);
                Assert.Equal(count, buffer.Count);
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
                Assert.Equal(count, buffer.Count);

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
        public void Dispose()
        {
            PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(42);
            buffer.Dispose();

            Assert.True(buffer.IsDisposedOrLostArrayOwnership);
        }

        [Fact]
        public void Slice()
        {
            Foo[] a = { new Foo() { A = 1, B = 2 }, new Foo() { A = 3, B = 4 } };
            
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(a))
            {
                var arrayPtr = buffer.Slice();

                Assert.Equal(a, arrayPtr.Array);
                Assert.Equal(0, arrayPtr.Offset);
                Assert.Equal(buffer.Pointer, arrayPtr.PointerAtOffset);
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