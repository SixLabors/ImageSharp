// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public partial class PixelOperationsTests
    {
        public class Rgba32 : PixelOperationsTests<ImageSharp.PixelFormats.Rgba32>
        {
            public Rgba32(ITestOutputHelper output)
                : base(output)
            {
            }

            // For 4.6 test runner MemberData does not work without redeclaring the public field in the derived test class:
            public static new TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };

            [Fact]
            public void IsSpecialImplementation()
            {
                Assert.IsType<ImageSharp.PixelFormats.Rgba32.PixelOperations>(PixelOperations<ImageSharp.PixelFormats.Rgba32>.Instance);
            }

            [Fact]
            public void ToVector4SimdAligned()
            {
                if (!Vector.IsHardwareAccelerated)
                {
                    return;
                }

                ImageSharp.PixelFormats.Rgba32[] source = CreatePixelTestData(64);
                Vector4[] expected = CreateExpectedVector4Data(source);

                TestOperation(
                    source,
                    expected,
                    (s, d) => ImageSharp.PixelFormats.Rgba32.PixelOperations.ToVector4SimdAligned(s, d.Span, 64)
                );
            }


            // [Fact] // Profiling benchmark - enable manually!
#pragma warning disable xUnit1013 // Public method should be marked as test
            public void Benchmark_ToVector4()
#pragma warning restore xUnit1013 // Public method should be marked as test
            {
                int times = 200000;
                int count = 1024;

                using (IBuffer<ImageSharp.PixelFormats.Rgba32> source = Configuration.Default.MemoryManager.Allocate<ImageSharp.PixelFormats.Rgba32>(count))
                using (IBuffer<Vector4> dest = Configuration.Default.MemoryManager.Allocate<Vector4>(count))
                {
                    this.Measure(
                        times,
                        () =>
                            {
                                PixelOperations<ImageSharp.PixelFormats.Rgba32>.Instance.ToVector4(source.Span, dest.Span, count);
                            });
                }
            }
        }

        public class Argb32 : PixelOperationsTests<ImageSharp.PixelFormats.Argb32>
        {
            // For 4.6 test runner MemberData does not work without redeclaring the public field in the derived test class:
            public Argb32(ITestOutputHelper output)
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
            var expected = new TPixel[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i].PackFromVector4(source[i]);
            }
            return expected;
        }

        internal static TPixel[] CreateScaledExpectedPixelData(Vector4[] source)
        {
            var expected = new TPixel[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i].PackFromScaledVector4(source[i]);
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
                (s, d) => Operations.PackFromVector4(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromScaledVector4(int count)
        {
            Vector4[] source = CreateVector4TestData(count);
            TPixel[] expected = CreateScaledExpectedPixelData(source);

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromScaledVector4(s, d.Span, count)
            );
        }

        internal static Vector4[] CreateExpectedVector4Data(TPixel[] source)
        {
            var expected = new Vector4[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = source[i].ToVector4();
            }
            return expected;
        }

        internal static Vector4[] CreateExpectedScaledVector4Data(TPixel[] source)
        {
            var expected = new Vector4[source.Length];

            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = source[i].ToScaledVector4();
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
                (s, d) => Operations.ToVector4(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToScaledVector4(int count)
        {
            TPixel[] source = CreateScaledPixelTestData(count);
            Vector4[] expected = CreateExpectedScaledVector4Data(source);

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToScaledVector4(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromXyzBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].PackFromRgba32(new Rgba32(source[i3 + 0], source[i3 + 1], source[i3 + 2], 255));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromRgb24Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToXyzBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 3];
            var rgb = default(Rgb24);

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                source[i].ToRgb24(ref rgb);
                expected[i3] = rgb.R;
                expected[i3 + 1] = rgb.G;
                expected[i3 + 2] = rgb.B;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgb24Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromXyzwBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromRgba32(new Rgba32(source[i4 + 0], source[i4 + 1], source[i4 + 2], source[i4 + 3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromRgba32Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToXyzwBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];
            var rgba = default(Rgba32);

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToRgba32(ref rgba);
                expected[i4] = rgba.R;
                expected[i4 + 1] = rgba.G;
                expected[i4 + 2] = rgba.B;
                expected[i4 + 3] = rgba.A;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgba32Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].PackFromRgba32(new Rgba32(source[i3 + 2], source[i3 + 1], source[i3 + 0], 255));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromBgr24Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToZyxBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 3];
            var bgr = default(Bgr24);

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                source[i].ToBgr24(ref bgr);
                expected[i3] = bgr.B;
                expected[i3 + 1] = bgr.G;
                expected[i3 + 2] = bgr.R;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToBgr24Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxwBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromRgba32(new Rgba32(source[i4 + 2], source[i4 + 1], source[i4 + 0], source[i4 + 3]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromBgra32Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToZyxwBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];
            var bgra = default(Bgra32);

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToBgra32(ref bgra);
                expected[i4] = bgra.B;
                expected[i4 + 1] = bgra.G;
                expected[i4 + 2] = bgra.R;
                expected[i4 + 3] = bgra.A;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToBgra32Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromWzyxBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromRgba32(new Rgba32(source[i4 + 1], source[i4 + 2], source[i4 + 3], source[i4 + 0]));
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromArgb32Bytes(s, d.Span, count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToWzyxBytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];
            var argb = default(Argb32);

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToArgb32(ref argb);
                expected[i4] = argb.A;
                expected[i4 + 1] = argb.R;
                expected[i4 + 2] = argb.G;
                expected[i4 + 3] = argb.B;
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToArgb32Bytes(s, d.Span, count)
            );
        }

        private class TestBuffers<TSource, TDest> : IDisposable
            where TSource : struct
            where TDest : struct
        {
            public TSource[] SourceBuffer { get; }
            public IBuffer<TDest> ActualDestBuffer { get; }
            public TDest[] ExpectedDestBuffer { get; }

            public TestBuffers(TSource[] source, TDest[] expectedDest)
            {
                this.SourceBuffer = source;
                this.ExpectedDestBuffer = expectedDest;
                this.ActualDestBuffer = Configuration.Default.MemoryManager.Allocate<TDest>(expectedDest.Length);
            }

            public void Dispose()
            {
                this.ActualDestBuffer.Dispose();
            }

            private const float Tolerance = 0.0001f;

            public void Verify()
            {
                int count = this.ExpectedDestBuffer.Length;

                if (typeof(TDest) == typeof(Vector4))
                {
                    Span<Vector4> expected = MemoryMarshal.Cast<TDest, Vector4>(this.ExpectedDestBuffer.AsSpan());
                    Span<Vector4> actual = MemoryMarshal.Cast<TDest, Vector4>(this.ActualDestBuffer.Span);

                    for (int i = 0; i < count; i++)
                    {
                        // ReSharper disable PossibleNullReferenceException
                        Assert.Equal(expected[i], actual[i], new ApproximateFloatComparer(0.001f));
                        // ReSharper restore PossibleNullReferenceException
                    }
                }
                else
                {
                    Span<TDest> expected = this.ExpectedDestBuffer.AsSpan();
                    Span<TDest> actual = this.ActualDestBuffer.Span;
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
            Action<TSource[], IBuffer<TDest>> action)
            where TSource : struct
            where TDest : struct
        {
            using (var buffers = new TestBuffers<TSource, TDest>(source, expected))
            {
                action(buffers.SourceBuffer, buffers.ActualDestBuffer);
                buffers.Verify();
            }
        }

        internal static Vector4[] CreateVector4TestData(int length)
        {
            var result = new Vector4[length];
            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = GetVector(rnd);
            }
            return result;
        }

        internal static TPixel[] CreatePixelTestData(int length)
        {
            var result = new TPixel[length];

            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                Vector4 v = GetVector(rnd);
                result[i].PackFromVector4(v);
            }

            return result;
        }

        internal static TPixel[] CreateScaledPixelTestData(int length)
        {
            var result = new TPixel[length];

            var rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                Vector4 v = GetVector(rnd);
                result[i].PackFromScaledVector4(v);
            }

            return result;
        }

        internal static byte[] CreateByteTestData(int length)
        {
            byte[] result = new byte[length];
            var rnd = new Random(42); // Deterministic random values

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