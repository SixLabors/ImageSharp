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
                Assert.Equal(array, buffer.Array);
                Assert.Equal(count, buffer.Count);

                VerifyPointer(buffer);
            }
        }

        [Fact]
        public void GetArrayPointer()
        {
            Foo[] a = { new Foo() { A = 1, B = 2 }, new Foo() { A = 3, B = 4 } };
            
            using (PinnedBuffer<Foo> buffer = new PinnedBuffer<Foo>(a))
            {
                var arrayPtr = buffer.GetArrayPointer();

                Assert.Equal(a, arrayPtr.Array);
                Assert.Equal(0, arrayPtr.Offset);
                Assert.Equal(buffer.Pointer, arrayPtr.PointerAtOffset);
            }
        }

        private static void VerifyPointer(PinnedBuffer<Foo> buffer)
        {
            IntPtr ptr = (IntPtr)Unsafe.AsPointer(ref buffer.Array[0]);
            Assert.Equal(ptr, buffer.Pointer);
        }
    }
}