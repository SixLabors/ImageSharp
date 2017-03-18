// ReSharper disable ObjectCreationAsStatement
// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Common
{
    using System;
    using System.Runtime.CompilerServices;

    using Xunit;

    public unsafe class BufferSpanTests
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
                BufferSpan<Foo> orig = colorBuf.Slice(1);
                BufferSpan<byte> asBytes = (BufferSpan < byte > )orig;

                Assert.Equal(asBytes.Start, sizeof(Foo));
                Assert.Equal(orig.PointerAtOffset, asBytes.PointerAtOffset);
            }
        }

        public class Construct
        {
            [Fact]
            public void Basic()
            {
                Foo[] array = Foo.CreateArray(3);
                fixed (Foo* p = array)
                {
                    // Act:
                    BufferSpan<Foo> span = new BufferSpan<Foo>(array, p);

                    // Assert:
                    Assert.Equal(array, span.Array);
                    Assert.Equal((IntPtr)p, span.PointerAtOffset);
                    Assert.Equal(3, span.Length);
                }
            }

            [Fact]
            public void WithStart()
            {
                Foo[] array = Foo.CreateArray(4);
                int start = 2;
                fixed (Foo* p = array)
                {
                    // Act:
                    BufferSpan<Foo> span = new BufferSpan<Foo>(array, p, start);

                    // Assert:
                    Assert.Equal(array, span.Array);
                    Assert.Equal(start, span.Start);
                    Assert.Equal((IntPtr)(p + start), span.PointerAtOffset);
                    Assert.Equal(array.Length - start, span.Length);
                }
            }

            [Fact]
            public void WithStartAndLength()
            {
                Foo[] array = Foo.CreateArray(10);
                int start = 2;
                int length = 3;
                fixed (Foo* p = array)
                {
                    // Act:
                    BufferSpan<Foo> span = new BufferSpan<Foo>(array, p, start, length);

                    // Assert:
                    Assert.Equal(array, span.Array);
                    Assert.Equal(start, span.Start);
                    Assert.Equal((IntPtr)(p + start), span.PointerAtOffset);
                    Assert.Equal(length, span.Length);
                }
            }
        }

        public class Slice
        {
            [Fact]
            public void StartOnly()
            {
                Foo[] array = Foo.CreateArray(5);
                int start0 = 2;
                int start1 = 2;
                int totalOffset = start0 + start1;

                fixed (Foo* p = array)
                {
                    BufferSpan<Foo> span = new BufferSpan<Foo>(array, p, start0);

                    // Act:
                    span = span.Slice(start1);

                    // Assert:
                    Assert.Equal(array, span.Array);
                    Assert.Equal(totalOffset, span.Start);
                    Assert.Equal((IntPtr)(p + totalOffset), span.PointerAtOffset);
                    Assert.Equal(array.Length - totalOffset, span.Length);
                }
            }

            [Fact]
            public void StartAndLength()
            {
                Foo[] array = Foo.CreateArray(10);
                int start0 = 2;
                int start1 = 2;
                int totalOffset = start0 + start1;
                int sliceLength = 3;

                fixed (Foo* p = array)
                {
                    BufferSpan<Foo> span = new BufferSpan<Foo>(array, p, start0);

                    // Act:
                    span = span.Slice(start1, sliceLength);

                    // Assert:
                    Assert.Equal(array, span.Array);
                    Assert.Equal(totalOffset, span.Start);
                    Assert.Equal((IntPtr)(p + totalOffset), span.PointerAtOffset);
                    Assert.Equal(sliceLength, span.Length);
                }
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
                BufferSpan<Foo> ap = new BufferSpan<Foo>(array, p, offset);

                // Act:
                ap.Clear(count);

                Assert.NotEqual(default(Foo), array[offset-1]);
                Assert.Equal(default(Foo), array[offset]);
                Assert.Equal(default(Foo), array[offset + count-1]);
                Assert.NotEqual(default(Foo), array[offset + count]);
            }
        }


        public class Indexer
        {
            public static readonly TheoryData<int, int, int> IndexerData =
                new TheoryData<int, int, int>()
                    {
                        { 10, 0, 0 },
                        { 10, 2, 0 },
                        { 16, 0, 3 },
                        { 16, 2, 3 },
                        { 10, 0, 9 },
                        { 10, 1, 8 }
                    };

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Read(int length, int start, int index)
            {
                Foo[] a = Foo.CreateArray(length);
                fixed (Foo* p = a)
                {
                    BufferSpan<Foo> span = new BufferSpan<Foo>(a, p, start);

                    Foo element = span[index];

                    Assert.Equal(a[start + index], element);
                }
            }

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Write(int length, int start, int index)
            {
                Foo[] a = Foo.CreateArray(length);
                fixed (Foo* p = a)
                {
                    BufferSpan<Foo> span = new BufferSpan<Foo>(a, p, start);

                    span[index] = new Foo(666, 666);

                    Assert.Equal(new Foo(666, 666), a[start + index]);
                }
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
                    BufferSpan<Foo> apSource = new BufferSpan<Foo>(source, pSource, 1);
                    BufferSpan<Foo> apDest = new BufferSpan<Foo>(dest, pDest, 1);

                    BufferSpan.Copy(apSource, apDest, count-1);
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
                    BufferSpan<AlignedFoo> apSource = new BufferSpan<AlignedFoo>(source, pSource, 1);
                    BufferSpan<AlignedFoo> apDest = new BufferSpan<AlignedFoo>(dest, pDest, 1);

                    BufferSpan.Copy(apSource, apDest, count - 1);
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
                    BufferSpan<int> apSource = new BufferSpan<int>(source, pSource, 1);
                    BufferSpan<int> apDest = new BufferSpan<int>(dest, pDest, 1);

                    BufferSpan.Copy(apSource, apDest, count -1);
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
                    BufferSpan<Foo> apSource = new BufferSpan<Foo>(source, pSource, 1);
                    BufferSpan<byte> apDest = new BufferSpan<byte>(dest, pDest, sizeof(Foo));

                    BufferSpan.Copy(apSource, apDest, count - 1);
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
                    BufferSpan<AlignedFoo> apSource = new BufferSpan<AlignedFoo>(source, pSource, 1);
                    BufferSpan<byte> apDest = new BufferSpan<byte>(dest, pDest, sizeof(AlignedFoo));

                    BufferSpan.Copy(apSource, apDest, count - 1);
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
                    BufferSpan<int> apSource = new BufferSpan<int>(source, pSource);
                    BufferSpan<byte> apDest = new BufferSpan<byte>(dest, pDest);

                    BufferSpan.Copy(apSource, apDest, count);
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
                    BufferSpan<byte> apSource = new BufferSpan<byte>(source, pSource);
                    BufferSpan<Foo> apDest = new BufferSpan<Foo>(dest, pDest);

                    BufferSpan.Copy(apSource, apDest, count);
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
                    BufferSpan.Copy<Color>(colorBuf, byteBuf, colorBuf.Length);

                    byte[] a = byteBuf.Array;

                    for (int i = 0; i < byteBuf.Length; i++)
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