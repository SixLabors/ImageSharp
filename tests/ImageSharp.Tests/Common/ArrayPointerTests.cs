// ReSharper disable ObjectCreationAsStatement
// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Common
{
    using System;

    using Xunit;

    public unsafe class ArrayPointerTests
    {
        public struct Foo
        {
            private int a;

            private double b;

            internal static Foo[] CreateArray(int size)
            {
                Foo[] result = new Foo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new Foo() { a = i, b = i };
                }
                return result;
            }
        }

        [Fact]
        public void ConstructWithNullArray_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () =>
                    {
                        new ArrayPointer<int>(null, (void*)0);
                    });

            Assert.Throws<ArgumentNullException>(
                () =>
                {
                    new ArrayPointer<int>(null, (void*)0);
                });
        }

        [Fact]
        public void ConstructWithoutOffset()
        {
            Foo[] array = Foo.CreateArray(3);
            fixed (Foo* p = array)
            {
                // Act:
                ArrayPointer<Foo> ap = new ArrayPointer<Foo>(array, p);

                // Assert:
                Assert.Equal(array, ap.Array);
                Assert.Equal((IntPtr)p, ap.PointerAtOffset);
            }
        }

        [Fact]
        public void ConstructWithOffset()
        {
            Foo[] array = Foo.CreateArray(3);
            int offset = 2;
            fixed (Foo* p = array)
            {
                // Act:
                ArrayPointer<Foo> ap = new ArrayPointer<Foo>(array, p, offset);

                // Assert:
                Assert.Equal(array, ap.Array);
                Assert.Equal(offset, ap.Offset);
                Assert.Equal((IntPtr)(p+offset), ap.PointerAtOffset);
            }
        }

        [Fact]
        public void Slice()
        {
            Foo[] array = Foo.CreateArray(5);
            int offset0 = 2;
            int offset1 = 2;
            int totalOffset = offset0 + offset1;
            fixed (Foo* p = array)
            {
                ArrayPointer<Foo> ap = new ArrayPointer<Foo>(array, p, offset0);

                // Act:
                ap = ap.Slice(offset1);

                // Assert:
                Assert.Equal(array, ap.Array);
                Assert.Equal(totalOffset, ap.Offset);
                Assert.Equal((IntPtr)(p + totalOffset), ap.PointerAtOffset);
            }
        }
    }
}