// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming
// ReSharper disable AccessToStaticMemberViaDerivedType
namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.Tests.Common;

    using Xunit;

    public unsafe class SpanUtilityTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class Assert : Xunit.Assert
        {
            public static void SameRefs<T1, T2>(ref T1 a, ref T2 b)
            {
                ref T1 bb = ref Unsafe.As<T2, T1>(ref b);

                Assert.True(Unsafe.AreSame(ref a, ref bb), "References are not same!");
            }
        }

        [Fact]
        public void FetchVector()
        {
            float[] stuff = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

            var span = new Span<float>(stuff);

            ref Vector<float> v = ref span.FetchVector();

            Assert.Equal(0, v[0]);
            Assert.Equal(1, v[1]);
            Assert.Equal(2, v[2]);
            Assert.Equal(3, v[3]);
        }
        
        public class SpanHelper_Copy
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
                TestStructs.Foo[] source = TestStructs.Foo.CreateArray(count + 2);
                TestStructs.Foo[] dest = new TestStructs.Foo[count + 5];

                var apSource = new Span<TestStructs.Foo>(source, 1, source.Length - 1);
                var apDest = new Span<TestStructs.Foo>(dest, 1, dest.Length - 1);

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
                TestStructs.AlignedFoo[] source = TestStructs.AlignedFoo.CreateArray(count + 2);
                TestStructs.AlignedFoo[] dest = new TestStructs.AlignedFoo[count + 5];

                var apSource = new Span<TestStructs.AlignedFoo>(source, 1, source.Length - 1);
                var apDest = new Span<TestStructs.AlignedFoo>(dest, 1, dest.Length - 1);

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

                var apSource = new Span<int>(source, 1, source.Length - 1);
                var apDest = new Span<int>(dest, 1, dest.Length - 1);

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
                int destCount = count * sizeof(TestStructs.Foo);
                TestStructs.Foo[] source = TestStructs.Foo.CreateArray(count + 2);
                byte[] dest = new byte[destCount + sizeof(TestStructs.Foo) * 2];

                var apSource = new Span<TestStructs.Foo>(source, 1, source.Length - 1);
                var apDest = new Span<byte>(dest, sizeof(TestStructs.Foo), dest.Length - sizeof(TestStructs.Foo));

                SpanHelper.Copy(apSource.AsBytes(), apDest, (count - 1) * sizeof(TestStructs.Foo));

                AssertNotDefault(source, 1);

                Assert.False((bool)ElementsAreEqual(source, dest, 0));
                Assert.True((bool)ElementsAreEqual(source, dest, 1));
                Assert.True((bool)ElementsAreEqual(source, dest, 2));
                Assert.True((bool)ElementsAreEqual(source, dest, count - 1));
                Assert.False((bool)ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void GenericToBytes_Aligned(int count)
            {
                int destCount = count * sizeof(TestStructs.Foo);
                TestStructs.AlignedFoo[] source = TestStructs.AlignedFoo.CreateArray(count + 2);
                byte[] dest = new byte[destCount + sizeof(TestStructs.AlignedFoo) * 2];

                var apSource = new Span<TestStructs.AlignedFoo>(source, 1, source.Length - 1);
                var apDest = new Span<byte>(dest, sizeof(TestStructs.AlignedFoo), dest.Length - sizeof(TestStructs.AlignedFoo));

                SpanHelper.Copy(apSource.AsBytes(), apDest, (count - 1) * sizeof(TestStructs.AlignedFoo));

                AssertNotDefault(source, 1);

                Assert.False((bool)ElementsAreEqual(source, dest, 0));
                Assert.True((bool)ElementsAreEqual(source, dest, 1));
                Assert.True((bool)ElementsAreEqual(source, dest, 2));
                Assert.True((bool)ElementsAreEqual(source, dest, count - 1));
                Assert.False((bool)ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void IntToBytes(int count)
            {
                int destCount = count * sizeof(int);
                int[] source = CreateTestInts(count + 2);
                byte[] dest = new byte[destCount + sizeof(int) + 1];

                var apSource = new Span<int>(source);
                var apDest = new Span<byte>(dest);

                SpanHelper.Copy(apSource.AsBytes(), apDest, count * sizeof(int));

                AssertNotDefault(source, 1);

                Assert.True((bool)ElementsAreEqual(source, dest, 0));
                Assert.True((bool)ElementsAreEqual(source, dest, count - 1));
                Assert.False((bool)ElementsAreEqual(source, dest, count));
            }

            [Theory]
            [InlineData(4)]
            [InlineData(1500)]
            public void BytesToGeneric(int count)
            {
                int srcCount = count * sizeof(TestStructs.Foo);
                byte[] source = CreateTestBytes(srcCount);
                TestStructs.Foo[] dest = new TestStructs.Foo[count + 2];

                var apSource = new Span<byte>(source);
                var apDest = new Span<TestStructs.Foo>(dest);

                SpanHelper.Copy(apSource, apDest.AsBytes(), count * sizeof(TestStructs.Foo));

                AssertNotDefault(source, sizeof(TestStructs.Foo) + 1);
                AssertNotDefault(dest, 1);

                Assert.True((bool)ElementsAreEqual(dest, source, 0));
                Assert.True((bool)ElementsAreEqual(dest, source, 1));
                Assert.True((bool)ElementsAreEqual(dest, source, count - 1));
                Assert.False((bool)ElementsAreEqual(dest, source, count));
            }
            
            internal static bool ElementsAreEqual(TestStructs.Foo[] array, byte[] rawArray, int index)
            {
                fixed (TestStructs.Foo* pArray = array)
                fixed (byte* pRaw = rawArray)
                {
                    TestStructs.Foo* pCasted = (TestStructs.Foo*)pRaw;

                    TestStructs.Foo val1 = pArray[index];
                    TestStructs.Foo val2 = pCasted[index];

                    return val1.Equals(val2);
                }
            }

            internal static bool ElementsAreEqual(TestStructs.AlignedFoo[] array, byte[] rawArray, int index)
            {
                fixed (TestStructs.AlignedFoo* pArray = array)
                fixed (byte* pRaw = rawArray)
                {
                    TestStructs.AlignedFoo* pCasted = (TestStructs.AlignedFoo*)pRaw;

                    TestStructs.AlignedFoo val1 = pArray[index];
                    TestStructs.AlignedFoo val2 = pCasted[index];

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