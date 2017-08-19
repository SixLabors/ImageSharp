// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using static SixLabors.ImageSharp.Tests.Common.TestStructs;

namespace SixLabors.ImageSharp.Tests.Common
{
    public unsafe class SpanTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class Assert : Xunit.Assert
        {
            public static void SameRefs<T1, T2>(ref T1 a, ref T2 b)
            {
                ref T1 bb = ref Unsafe.As<T2, T1>(ref b);

                True(Unsafe.AreSame(ref a, ref bb), "References are not same!");
            }
        }

        [Fact]
        public void FetchVector()
        {
            float[] stuff = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

            Span<float> span = new Span<float>(stuff);

            ref Vector<float> v = ref span.FetchVector();

            Assert.Equal(0, v[0]);
            Assert.Equal(1, v[1]);
            Assert.Equal(2, v[2]);
            Assert.Equal(3, v[3]);
        }

        [Fact]
        public void AsBytes()
        {
            Foo[] fooz = { new Foo(1, 2), new Foo(3, 4), new Foo(5, 6) };

            using (Buffer<Foo> colorBuf = new Buffer<Foo>(fooz))
            {
                Span<Foo> orig = colorBuf.Slice(1);
                Span<byte> asBytes = orig.AsBytes();

                //  Assert.Equal(asBytes.Start, sizeof(Foo));
                Assert.Equal(orig.Length * Unsafe.SizeOf<Foo>(), asBytes.Length);
                Assert.SameRefs(ref orig.DangerousGetPinnableReference(), ref asBytes.DangerousGetPinnableReference());
            }
        }

        public class Construct
        {
            [Fact]
            public void Basic()
            {
                Foo[] array = Foo.CreateArray(3);

                // Act:
                Span<Foo> span = new Span<Foo>(array);

                // Assert:
                Assert.Equal(array, span.ToArray());
                Assert.Equal(3, span.Length);
                Assert.SameRefs(ref array[0], ref span.DangerousGetPinnableReference());
            }

            [Fact]
            public void WithStart()
            {
                Foo[] array = Foo.CreateArray(4);
                int start = 2;

                // Act:
                Span<Foo> span = new Span<Foo>(array, start);

                // Assert:
                Assert.SameRefs(ref array[start], ref span.DangerousGetPinnableReference());
                Assert.Equal(array.Length - start, span.Length);
            }

            [Fact]
            public void WithStartAndLength()
            {
                Foo[] array = Foo.CreateArray(10);
                int start = 2;
                int length = 3;
                // Act:
                Span<Foo> span = new Span<Foo>(array, start, length);

                // Assert:
                Assert.SameRefs(ref array[start], ref span.DangerousGetPinnableReference());
                Assert.Equal(length, span.Length);
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

                Span<Foo> span = new Span<Foo>(array, start0);

                // Act:
                span = span.Slice(start1);

                // Assert:
                Assert.SameRefs(ref array[totalOffset], ref span.DangerousGetPinnableReference());
                Assert.Equal(array.Length - totalOffset, span.Length);
            }

            [Fact]
            public void StartAndLength()
            {
                Foo[] array = Foo.CreateArray(10);
                int start0 = 2;
                int start1 = 2;
                int totalOffset = start0 + start1;
                int sliceLength = 3;

                Span<Foo> span = new Span<Foo>(array, start0);

                // Act:
                span = span.Slice(start1, sliceLength);

                // Assert:
                Assert.SameRefs(ref array[totalOffset], ref span.DangerousGetPinnableReference());
                Assert.Equal(sliceLength, span.Length);
            }
        }

        //[Theory]
        //[InlineData(4)]
        //[InlineData(1500)]
        //public void Clear(int count)
        //{
        //    Foo[] array = Foo.CreateArray(count + 42);

        //    int offset = 2;
        //    Span<Foo> ap = new Span<Foo>(array, offset);

        //    // Act:
        //    ap.Clear(count);

        //    Assert.NotEqual(default(Foo), array[offset - 1]);
        //    Assert.Equal(default(Foo), array[offset]);
        //    Assert.Equal(default(Foo), array[offset + count - 1]);
        //    Assert.NotEqual(default(Foo), array[offset + count]);
        //}

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
                Span<Foo> span = new Span<Foo>(a, start);

                Foo element = span[index];

                Assert.Equal(a[start + index], element);
            }

            [Theory]
            [MemberData(nameof(IndexerData))]
            public void Write(int length, int start, int index)
            {
                Foo[] a = Foo.CreateArray(length);
                Span<Foo> span = new Span<Foo>(a, start);

                span[index] = new Foo(666, 666);

                Assert.Equal(new Foo(666, 666), a[start + index]);
            }

            [Theory]
            [InlineData(10, 0, 0, 5)]
            [InlineData(10, 1, 1, 5)]
            [InlineData(10, 1, 1, 6)]
            [InlineData(10, 1, 1, 7)]
            public void AsBytes_Read(int length, int start, int index, int byteOffset)
            {
                Foo[] a = Foo.CreateArray(length);
                Span<Foo> span = new Span<Foo>(a, start);

                Span<byte> bytes = span.AsBytes();

                byte actual = bytes[index * Unsafe.SizeOf<Foo>() + byteOffset];

                ref byte baseRef = ref Unsafe.As<Foo, byte>(ref a[0]);
                byte expected = Unsafe.Add(ref baseRef, (start + index) * Unsafe.SizeOf<Foo>() + byteOffset);

                Assert.Equal(expected, actual);
            }
        }

        [Theory]
        [InlineData(0, 4)]
        [InlineData(2, 4)]
        [InlineData(3, 4)]
        public void DangerousGetPinnableReference(int start, int length)
        {
            Foo[] a = Foo.CreateArray(length);
            Span<Foo> span = new Span<Foo>(a, start);
            ref Foo r = ref span.DangerousGetPinnableReference();

            Assert.True(Unsafe.AreSame(ref a[start], ref r));
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

                Span<Foo> apSource = new Span<Foo>(source, 1);
                Span<Foo> apDest = new Span<Foo>(dest, 1);

                SpanHelper.Copy(apSource, apDest, count - 1);

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
            public void GenericToOwnType_Aligned(int count)
            {
                AlignedFoo[] source = AlignedFoo.CreateArray(count + 2);
                AlignedFoo[] dest = new AlignedFoo[count + 5];

                Span<AlignedFoo> apSource = new Span<AlignedFoo>(source, 1);
                Span<AlignedFoo> apDest = new Span<AlignedFoo>(dest, 1);

                SpanHelper.Copy(apSource, apDest, count - 1);

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
                int[] source = CreateTestInts(count + 2);
                int[] dest = new int[count + 5];

                Span<int> apSource = new Span<int>(source, 1);
                Span<int> apDest = new Span<int>(dest, 1);

                SpanHelper.Copy(apSource, apDest, count - 1);

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
                Foo[] source = Foo.CreateArray(count + 2);
                byte[] dest = new byte[destCount + sizeof(Foo) * 2];

                Span<Foo> apSource = new Span<Foo>(source, 1);
                Span<byte> apDest = new Span<byte>(dest, sizeof(Foo));

                SpanHelper.Copy(apSource.AsBytes(), apDest, (count - 1) * sizeof(Foo));

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

                Span<AlignedFoo> apSource = new Span<AlignedFoo>(source, 1);
                Span<byte> apDest = new Span<byte>(dest, sizeof(AlignedFoo));

                SpanHelper.Copy(apSource.AsBytes(), apDest, (count - 1) * sizeof(AlignedFoo));

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
                int[] source = CreateTestInts(count + 2);
                byte[] dest = new byte[destCount + sizeof(int) + 1];

                Span<int> apSource = new Span<int>(source);
                Span<byte> apDest = new Span<byte>(dest);

                SpanHelper.Copy(apSource.AsBytes(), apDest, count * sizeof(int));

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

                Span<byte> apSource = new Span<byte>(source);
                Span<Foo> apDest = new Span<Foo>(dest);

                SpanHelper.Copy(apSource, apDest.AsBytes(), count * sizeof(Foo));

                AssertNotDefault(source, sizeof(Foo) + 1);
                AssertNotDefault(dest, 1);

                Assert.True(ElementsAreEqual(dest, source, 0));
                Assert.True(ElementsAreEqual(dest, source, 1));
                Assert.True(ElementsAreEqual(dest, source, count - 1));
                Assert.False(ElementsAreEqual(dest, source, count));
            }

            [Fact]
            public void Color32ToBytes()
            {
                Rgba32[] colors = { new Rgba32(0, 1, 2, 3), new Rgba32(4, 5, 6, 7), new Rgba32(8, 9, 10, 11), };

                using (Buffer<Rgba32> colorBuf = new Buffer<Rgba32>(colors))
                using (Buffer<byte> byteBuf = new Buffer<byte>(colors.Length * 4))
                {
                    SpanHelper.Copy(colorBuf.Span.AsBytes(), byteBuf, colorBuf.Length * sizeof(Rgba32));

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