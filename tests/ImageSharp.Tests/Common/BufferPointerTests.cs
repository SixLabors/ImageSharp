// ReSharper disable ObjectCreationAsStatement
// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Common
{
    using System;
    using System.Runtime.CompilerServices;

    using Xunit;

    public unsafe class BufferPointerTests
    {
        public struct Foo
        {
            public int A;

            public double B;

            internal static Foo[] CreateArray(int size)
            {
                Foo[] result = new Foo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new Foo() { A = i, B = i };
                }
                return result;
            }
        }
        
        [Fact]
        public void ConstructWithoutOffset()
        {
            Foo[] array = Foo.CreateArray(3);
            fixed (Foo* p = array)
            {
                // Act:
                BufferPointer<Foo> ap = new BufferPointer<Foo>(array, p);

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
                BufferPointer<Foo> ap = new BufferPointer<Foo>(array, p, offset);

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
                BufferPointer<Foo> ap = new BufferPointer<Foo>(array, p, offset0);

                // Act:
                ap = ap.Slice(offset1);

                // Assert:
                Assert.Equal(array, ap.Array);
                Assert.Equal(totalOffset, ap.Offset);
                Assert.Equal((IntPtr)(p + totalOffset), ap.PointerAtOffset);
            }
        }

        public class Copy
        {
            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void GenericToOwnType(int count)
            {
                Foo[] source = Foo.CreateArray(count + 2);
                Foo[] dest = new Foo[count + 5];

                fixed (Foo* pSource = source)
                fixed (Foo* pDest = dest)
                {
                    BufferPointer<Foo> apSource = new BufferPointer<Foo>(source, pSource);
                    BufferPointer<Foo> apDest = new BufferPointer<Foo>(dest, pDest);

                    BufferPointer.Copy(apSource, apDest, count);
                }

                Assert.Equal(source[0], dest[0]);
                Assert.Equal(source[count-1], dest[count-1]);
                Assert.NotEqual(source[count], dest[count]);
            }
            
            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void GenericToBytes(int count)
            {
                int destCount = count * sizeof(Foo);
                Foo[] source = Foo.CreateArray(count + 2);
                byte[] dest = new byte[destCount + sizeof(Foo) + 1];

                fixed (Foo* pSource = source)
                fixed (byte* pDest = dest)
                {
                    BufferPointer<Foo> apSource = new BufferPointer<Foo>(source, pSource);
                    BufferPointer<byte> apDest = new BufferPointer<byte>(dest, pDest);

                    BufferPointer.Copy(apSource, apDest, count);
                }

                Assert.True(ElementsAreEqual(source, dest, 0));
                Assert.True(ElementsAreEqual(source, dest, count - 1));
                Assert.False(ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void BytesToGeneric(int count)
            {
                int destCount = count * sizeof(Foo);
                byte[] source = new byte[destCount + sizeof(Foo) + 1];
                Foo[] dest = Foo.CreateArray(count + 2);
                
                fixed(byte* pSource = source)
                fixed (Foo* pDest = dest)
                {
                    BufferPointer<byte> apSource = new BufferPointer<byte>(source, pSource);
                    BufferPointer<Foo> apDest = new BufferPointer<Foo>(dest, pDest);

                    BufferPointer.Copy(apSource, apDest, count);
                }

                Assert.True(ElementsAreEqual(dest, source, 0));
                Assert.True(ElementsAreEqual(dest, source, count - 1));
                Assert.False(ElementsAreEqual(dest, source, count));
            }
            
            private static bool ElementsAreEqual(Foo[] array, byte[] rawArray, int index)
            {
                fixed (Foo* pArray = array)
                fixed (byte* pRaw = rawArray)
                {
                    Foo* pCasted = (Foo*)pRaw;

                    Foo val1 = pArray[index];
                    Foo val2 = pCasted[index];

                    return val1.Equals(val2);
                }
            }
        }
    }
}