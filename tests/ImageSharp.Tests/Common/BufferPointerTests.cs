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

            public Foo(int a, double b)
            {
                this.A = a;
                this.B = b;
            }

            internal static Foo[] CreateArray(int size)
            {
                Foo[] result = new Foo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new Foo(i+1, i+1);
                }
                return result;
            }
        }

        /// <summary>
        /// sizeof(AlignedFoo) == sizeof(long)
        /// </summary>
        public struct AlignedFoo
        {
            public int A;

            public int B;

            static AlignedFoo()
            {
                Assert.Equal(sizeof(AlignedFoo), sizeof(long));
            }

            public AlignedFoo(int a, int b)
            {
                this.A = a;
                this.B = b;
            }

            internal static AlignedFoo[] CreateArray(int size)
            {
                AlignedFoo[] result = new AlignedFoo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new AlignedFoo(i + 1, i + 1);
                }
                return result;
            }
        }

        [Fact]
        public void AsBytes()
        {
            Foo[] fooz = { new Foo(1, 2), new Foo(3, 4), new Foo(5, 6) };

            using (PinnedBuffer<Foo> colorBuf = new PinnedBuffer<Foo>(fooz))
            {
                BufferPointer<Foo> orig = colorBuf.Slice(1);
                BufferPointer<byte> asBytes = (BufferPointer < byte > )orig;

                Assert.Equal(asBytes.Offset, sizeof(Foo));
                Assert.Equal(orig.PointerAtOffset, asBytes.PointerAtOffset);
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


        [Theory]
        [InlineData(4)]
        [InlineData(1500)]
        public void Clear(int count)
        {
            Foo[] array = Foo.CreateArray(count + 42);

            int offset = 2;
            fixed (Foo* p = array)
            {
                BufferPointer<Foo> ap = new BufferPointer<Foo>(array, p, offset);

                // Act:
                ap.Clear(count);
            }
        }


        public class Copy
        {
            private static void AssertNotDefault<T>(T[] data, int idx)
                where T : struct
            {
                Assert.NotEqual(default(T), data[idx]);
            }

            private static byte[] CreateTestBytes(int count)
            {
                byte[] result = new byte[count];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = (byte)((i % 200) + 1);
                }
                return result;
            }

            private static int[] CreateTestInts(int count)
            {
                int[] result = new int[count];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = i + 1;
                }
                return result;
            }

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
                    BufferPointer<Foo> apSource = new BufferPointer<Foo>(source, pSource, 1);
                    BufferPointer<Foo> apDest = new BufferPointer<Foo>(dest, pDest, 1);

                    BufferPointer.Copy(apSource, apDest, count-1);
                }

                AssertNotDefault(source, 1);
                AssertNotDefault(dest, 1);

                Assert.NotEqual(source[0], dest[0]);
                Assert.Equal(source[1], dest[1]);
                Assert.Equal(source[2], dest[2]);
                Assert.Equal(source[count-1], dest[count-1]);
                Assert.NotEqual(source[count], dest[count]);
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void GenericToOwnType_Aligned(int count)
            {
                AlignedFoo[] source = AlignedFoo.CreateArray(count + 2);
                AlignedFoo[] dest = new AlignedFoo[count + 5];

                fixed (AlignedFoo* pSource = source)
                fixed (AlignedFoo* pDest = dest)
                {
                    BufferPointer<AlignedFoo> apSource = new BufferPointer<AlignedFoo>(source, pSource, 1);
                    BufferPointer<AlignedFoo> apDest = new BufferPointer<AlignedFoo>(dest, pDest, 1);

                    BufferPointer.Copy(apSource, apDest, count - 1);
                }

                AssertNotDefault(source, 1);
                AssertNotDefault(dest, 1);

                Assert.NotEqual(source[0], dest[0]);
                Assert.Equal(source[1], dest[1]);
                Assert.Equal(source[2], dest[2]);
                Assert.Equal(source[count - 1], dest[count - 1]);
                Assert.NotEqual(source[count], dest[count]);
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void IntToInt(int count)
            {
                int[] source = CreateTestInts(count+2);
                int[] dest = new int[count + 5];

                fixed (int* pSource = source)
                fixed (int* pDest = dest)
                {
                    BufferPointer<int> apSource = new BufferPointer<int>(source, pSource, 1);
                    BufferPointer<int> apDest = new BufferPointer<int>(dest, pDest, 1);

                    BufferPointer.Copy(apSource, apDest, count -1);
                }

                AssertNotDefault(source, 1);
                AssertNotDefault(dest, 1);

                Assert.NotEqual(source[0], dest[0]);
                Assert.Equal(source[1], dest[1]);
                Assert.Equal(source[2], dest[2]);
                Assert.Equal(source[count - 1], dest[count - 1]);
                Assert.NotEqual(source[count], dest[count]);
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void GenericToBytes(int count)
            {
                int destCount = count * sizeof(Foo);
                Foo[] source = Foo.CreateArray(count+2);
                byte[] dest = new byte[destCount + sizeof(Foo)*2];

                fixed (Foo* pSource = source)
                fixed (byte* pDest = dest)
                {
                    BufferPointer<Foo> apSource = new BufferPointer<Foo>(source, pSource, 1);
                    BufferPointer<byte> apDest = new BufferPointer<byte>(dest, pDest, sizeof(Foo));

                    BufferPointer.Copy(apSource, apDest, count - 1);
                }

                AssertNotDefault(source, 1);

                Assert.False(ElementsAreEqual(source, dest, 0));
                Assert.True(ElementsAreEqual(source, dest, 1));
                Assert.True(ElementsAreEqual(source, dest, 2));
                Assert.True(ElementsAreEqual(source, dest, count - 1));
                Assert.False(ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void GenericToBytes_Aligned(int count)
            {
                int destCount = count * sizeof(Foo);
                AlignedFoo[] source = AlignedFoo.CreateArray(count + 2);
                byte[] dest = new byte[destCount + sizeof(AlignedFoo) * 2];

                fixed (AlignedFoo* pSource = source)
                fixed (byte* pDest = dest)
                {
                    BufferPointer<AlignedFoo> apSource = new BufferPointer<AlignedFoo>(source, pSource, 1);
                    BufferPointer<byte> apDest = new BufferPointer<byte>(dest, pDest, sizeof(AlignedFoo));

                    BufferPointer.Copy(apSource, apDest, count - 1);
                }

                AssertNotDefault(source, 1);

                Assert.False(ElementsAreEqual(source, dest, 0));
                Assert.True(ElementsAreEqual(source, dest, 1));
                Assert.True(ElementsAreEqual(source, dest, 2));
                Assert.True(ElementsAreEqual(source, dest, count - 1));
                Assert.False(ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void IntToBytes(int count)
            {
                int destCount = count * sizeof(int);
                int[] source = CreateTestInts(count+2);
                byte[] dest = new byte[destCount + sizeof(int) + 1];

                fixed (int* pSource = source)
                fixed (byte* pDest = dest)
                {
                    BufferPointer<int> apSource = new BufferPointer<int>(source, pSource);
                    BufferPointer<byte> apDest = new BufferPointer<byte>(dest, pDest);

                    BufferPointer.Copy(apSource, apDest, count);
                }

                AssertNotDefault(source, 1);

                Assert.True(ElementsAreEqual(source, dest, 0));
                Assert.True(ElementsAreEqual(source, dest, count - 1));
                Assert.False(ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void BytesToGeneric(int count)
            {
                int srcCount = count * sizeof(Foo);
                byte[] source = CreateTestBytes(srcCount);
                Foo[] dest = new Foo[count + 2];
                
                fixed(byte* pSource = source)
                fixed (Foo* pDest = dest)
                {
                    BufferPointer<byte> apSource = new BufferPointer<byte>(source, pSource);
                    BufferPointer<Foo> apDest = new BufferPointer<Foo>(dest, pDest);

                    BufferPointer.Copy(apSource, apDest, count);
                }

                AssertNotDefault(source, sizeof(Foo) + 1);
                AssertNotDefault(dest, 1);

                Assert.True(ElementsAreEqual(dest, source, 0));
                Assert.True(ElementsAreEqual(dest, source, 1));
                Assert.True(ElementsAreEqual(dest, source, count - 1));
                Assert.False(ElementsAreEqual(dest, source, count));
            }

            [Fact]
            public void ColorToBytes()
            {
                Color[] colors = { new Color(0, 1, 2, 3), new Color(4, 5, 6, 7), new Color(8, 9, 10, 11), };

                using (PinnedBuffer<Color> colorBuf = new PinnedBuffer<Color>(colors))
                using (PinnedBuffer<byte> byteBuf = new PinnedBuffer<byte>(colors.Length*4))
                {
                    BufferPointer.Copy<Color>(colorBuf, byteBuf, colorBuf.Count);

                    byte[] a = byteBuf.Array;

                    for (int i = 0; i < byteBuf.Count; i++)
                    {
                        Assert.Equal((byte)i, a[i]);
                    }
                }
            }

            internal static bool ElementsAreEqual(Foo[] array, byte[] rawArray, int index)
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

            internal static bool ElementsAreEqual(AlignedFoo[] array, byte[] rawArray, int index)
            {
                fixed (AlignedFoo* pArray = array)
                fixed (byte* pRaw = rawArray)
                {
                    AlignedFoo* pCasted = (AlignedFoo*)pRaw;

                    AlignedFoo val1 = pArray[index];
                    AlignedFoo val2 = pCasted[index];

                    return val1.Equals(val2);
                }
            }

            internal static bool ElementsAreEqual(int[] array, byte[] rawArray, int index)
            {
                fixed (int* pArray = array)
                fixed (byte* pRaw = rawArray)
                {
                    int* pCasted = (int*)pRaw;

                    int val1 = pArray[index];
                    int val2 = pCasted[index];

                    return val1.Equals(val2);
                }
            }
        }
    }
}