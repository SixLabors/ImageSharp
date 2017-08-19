// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public partial class PixelOperationsTests
    {

        public class Color32 : PixelOperationsTests<Rgba32>
        {
            public Color32(ITestOutputHelper output)
                : base(output)
            {
            }

            // For 4.6 test runner MemberData does not work without redeclaring the public field in the derived test class:
            public static new TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };

            [Fact]
            public void IsSpecialImplementation()
            {
                Assert.IsType<Rgba32.PixelOperations>(PixelOperations<Rgba32>.Instance);
            }

            [Fact]
            public void ToVector4SimdAligned()
            {
                Rgba32[] source = CreatePixelTestData(64);
                Vector4[] expected = CreateExpectedVector4Data(source);

                TestOperation(
                    source,
                    expected,
                    (s, d) => Rgba32.PixelOperations.ToVector4SimdAligned(s, d, 64)
                );
            }


            // [Fact] // Profiling benchmark - enable manually!
#pragma warning disable xUnit1013 // Public method should be marked as test
            public void Benchmark_ToVector4()
#pragma warning restore xUnit1013 // Public method should be marked as test
            {
                int times = 200000;
                int count = 1024;

                using (Buffer<Rgba32> source = new Buffer<Rgba32>(count))
                using (Buffer<Vector4> dest = new Buffer<Vector4>(count))
                {
                    this.Measure(
                        times,
                        () =>
                            {
                                PixelOperations<Rgba32>.Instance.ToVector4(source, dest, count);
                            });
                }
            }
        }

        public class Argb : PixelOperationsTests<Argb32>
        {
            // For 4.6 test runner MemberData does not work without redeclaring the public field in the derived test class:
            public Argb(ITestOutputHelper output)
                : base(output)
            {
            }

            public static new TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.All)]
        public void GetGlobalInstance<TPixel>(TestImageProvider<TPixel> dummy)
            where TPixel : struct, IPixel<TPixel>
        {
            Assert.NotNull(PixelOperations<TPixel>.Instance);
        }
    }

    public abstract class PixelOperationsTests<TPixel> : MeasureFixture
        where TPixel : struct, IPixel<TPixel>
    {
        protected PixelOperationsTests(ITestOutputHelper output)
            : base(output)
        {
        }

        public static TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };

        private static PixelOperations<TPixel> Operations => PixelOperations<TPixel>.Instance;

        internal static TPixel[] CreateExpectedPixelData(Vector4[] source)
        {
            TPixel[] expected = new TPixel[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i].PackFromVector4(source[i]);
            }
            return expected;
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromVector4(int count)
        {
            Vector4[] source = CreateVector4TestData(count);
            TPixel[] expected = CreateExpectedPixelData(source);

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromVector4(s, d, count)
            );
        }

        internal static Vector4[] CreateExpectedVector4Data(TPixel[] source)
        {
            Vector4[] expected = new Vector4[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = source[i].ToVector4();
            }
            return expected;
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToVector4(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            Vector4[] expected = CreateExpectedVector4Data(source);

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToVector4(s, d, count)
            );
        }


        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromXyzBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            TPixel[] expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].PackFromRgba32(new Rgba32(source[i3 + 0], source[i3 + 1], source[i3 + 2], 255));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromRgb24Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToXyzBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 3];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                source[i].ToXyzBytes(expected, i3);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgb24Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromXyzwBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            TPixel[] expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromRgba32(new Rgba32(source[i4 + 0], source[i4 + 1], source[i4 + 2], source[i4 + 3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromRgba32Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToXyzwBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToXyzwBytes(expected, i4);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgba32Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            TPixel[] expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].PackFromRgba32(new Rgba32(source[i3 + 2], source[i3 + 1], source[i3 + 0], 255));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromBgr24Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToZyxBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 3];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                source[i].ToZyxBytes(expected, i3);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToBgr24Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxwBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            TPixel[] expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromRgba32(new Rgba32(source[i4 + 2], source[i4 + 1], source[i4 + 0], source[i4 + 3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromBgra32Bytes(s, d, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToZyxwBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToZyxwBytes(expected, i4);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToBgra32Bytes(s, d, count)
            );
        }


        private class TestBuffers<TSource, TDest> : IDisposable
            where TSource : struct
            where TDest : struct
        {
            public Buffer<TSource> SourceBuffer { get; }
            public Buffer<TDest> ActualDestBuffer { get; }
            public Buffer<TDest> ExpectedDestBuffer { get; }

            public Span<TSource> Source => this.SourceBuffer;
            public Span<TDest> ActualDest => this.ActualDestBuffer;

            public TestBuffers(TSource[] source, TDest[] expectedDest)
            {
                this.SourceBuffer = new Buffer<TSource>(source);
                this.ExpectedDestBuffer = new Buffer<TDest>(expectedDest);
                this.ActualDestBuffer = new Buffer<TDest>(expectedDest.Length);
            }

            public void Dispose()
            {
                this.SourceBuffer.Dispose();
                this.ActualDestBuffer.Dispose();
                this.ExpectedDestBuffer.Dispose();
            }

            private const float Tolerance = 0.0001f;

            public void Verify()
            {
                int count = this.ExpectedDestBuffer.Length;

                if (typeof(TDest) == typeof(Vector4))
                {
                    Vector4[] expected = this.ExpectedDestBuffer.Array as Vector4[];
                    Vector4[] actual = this.ActualDestBuffer.Array as Vector4[];

                    for (int i = 0; i < count; i++)
                    {
                        // ReSharper disable PossibleNullReferenceException
                        Assert.Equal(expected[i], actual[i], new ApproximateFloatComparer(0.001f));
                        // ReSharper restore PossibleNullReferenceException
                    }
                }
                else
                {
                    TDest[] expected = this.ExpectedDestBuffer.Array;
                    TDest[] actual = this.ActualDestBuffer.Array;
                    for (int i = 0; i < count; i++)
                    {
                        Assert.Equal(expected[i], actual[i]);
                    }
                }
            }
        }

        internal static void TestOperation<TSource, TDest>(
            TSource[] source,
            TDest[] expected,
            Action<Span<TSource>, Buffer<TDest>> action)
            where TSource : struct
            where TDest : struct
        {
            using (TestBuffers<TSource, TDest> buffers = new TestBuffers<TSource, TDest>(source, expected))
            {
                action(buffers.Source, buffers.ActualDestBuffer);
                buffers.Verify();
            }
        }

        internal static Vector4[] CreateVector4TestData(int length)
        {
            Vector4[] result = new Vector4[length];
            Random rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = GetVector(rnd);
            }
            return result;
        }

        internal static TPixel[] CreatePixelTestData(int length)
        {
            TPixel[] result = new TPixel[length];

            Random rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                Vector4 v = GetVector(rnd);
                result[i].PackFromVector4(v);
            }

            return result;
        }

        internal static byte[] CreateByteTestData(int length)
        {
            byte[] result = new byte[length];
            Random rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)rnd.Next(255);
            }
            return result;
        }

        internal static Vector4 GetVector(Random rnd)
        {
            return new Vector4(
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble(),
                (float)rnd.NextDouble()
            );
        }
    }
}