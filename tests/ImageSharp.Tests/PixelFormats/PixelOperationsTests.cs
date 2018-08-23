// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
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
                    (s, d) => ImageSharp.PixelFormats.Rgba32.PixelOperations.ToVector4SimdAligned(s, d.GetSpan(), 64)
                );
            }


            // [Fact] // Profiling benchmark - enable manually!
#pragma warning disable xUnit1013 // Public method should be marked as test
            public void Benchmark_ToVector4()
#pragma warning restore xUnit1013 // Public method should be marked as test
            {
                int times = 200000;
                int count = 1024;

                using (IMemoryOwner<ImageSharp.PixelFormats.Rgba32> source = Configuration.Default.MemoryAllocator.Allocate<ImageSharp.PixelFormats.Rgba32>(count))
                using (IMemoryOwner<Vector4> dest = Configuration.Default.MemoryAllocator.Allocate<Vector4>(count))
                {
                    this.Measure(
                        times,
                        () =>
                            {
                                PixelOperations<ImageSharp.PixelFormats.Rgba32>.Instance.ToVector4(source.GetSpan(), dest.GetSpan(), count);
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

        [Fact]
        public void IsOpaqueColor()
        {
            Assert.True(new GraphicsOptions(true).IsOpaqueColorWithoutBlending(ImageSharp.PixelFormats.Rgba32.Red));

            Assert.False(new GraphicsOptions(true, 0.5f).IsOpaqueColorWithoutBlending(ImageSharp.PixelFormats.Rgba32.Red));
            Assert.False(new GraphicsOptions(true).IsOpaqueColorWithoutBlending(ImageSharp.PixelFormats.Rgba32.Transparent));
            Assert.False(new GraphicsOptions(true, PixelColorBlendingMode.Lighten, 1).IsOpaqueColorWithoutBlending(ImageSharp.PixelFormats.Rgba32.Red));
            Assert.False(new GraphicsOptions(true, PixelColorBlendingMode.Normal,PixelAlphaCompositionMode.DestOver, 1).IsOpaqueColorWithoutBlending(ImageSharp.PixelFormats.Rgba32.Red));
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
                (s, d) => Operations.PackFromVector4(s, d.GetSpan(), count)
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
                (s, d) => Operations.PackFromScaledVector4(s, d.GetSpan(), count)
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
                (s, d) => Operations.ToVector4(s, d.GetSpan(), count)
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
                (s, d) => Operations.ToScaledVector4(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromRgb24Bytes(int count)
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
                (s, d) => Operations.PackFromRgb24Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgb24Bytes(int count)
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
                (s, d) => Operations.ToRgb24Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromRgba32Bytes(int count)
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
                (s, d) => Operations.PackFromRgba32Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgba32Bytes(int count)
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
                (s, d) => Operations.ToRgba32Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromRgb48Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 6);
            Span<byte> sourceSpan = source.AsSpan();
            var expected = new TPixel[count];

            var rgba64 = new Rgba64(0, 0, 0, 65535);
            for (int i = 0; i < count; i++)
            {
                int i6 = i * 6;
                rgba64.Rgb = MemoryMarshal.Cast<byte, Rgb48>(sourceSpan.Slice(i6, 6))[0];
                expected[i].PackFromRgba64(rgba64);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromRgb48Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgb48Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 6];
            Rgb48 rgb = default;

            for (int i = 0; i < count; i++)
            {
                int i6 = i * 6;
                source[i].ToRgb48(ref rgb);
                Rgba64Bytes rgb48Bytes = Unsafe.As<Rgb48, Rgba64Bytes>(ref rgb);
                expected[i6] = rgb48Bytes[0];
                expected[i6 + 1] = rgb48Bytes[1];
                expected[i6 + 2] = rgb48Bytes[2];
                expected[i6 + 3] = rgb48Bytes[3];
                expected[i6 + 4] = rgb48Bytes[4];
                expected[i6 + 5] = rgb48Bytes[5];
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgb48Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromRgba64Bytes(int count)
        {
            byte[] source = CreateByteTestData(count * 8);
            Span<byte> sourceSpan = source.AsSpan();
            var expected = new TPixel[count];

            for (int i = 0; i < count; i++)
            {
                int i8 = i * 8;
                expected[i].PackFromRgba64(MemoryMarshal.Cast<byte, Rgba64>(sourceSpan.Slice(i8, 8))[0]);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromRgba64Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToRgba64Bytes(int count)
        {
            TPixel[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 8];
            Rgba64 rgba = default;

            for (int i = 0; i < count; i++)
            {
                int i8 = i * 8;
                source[i].ToRgba64(ref rgba);
                Rgba64Bytes rgba64Bytes = Unsafe.As<Rgba64, Rgba64Bytes>(ref rgba);
                expected[i8] = rgba64Bytes[0];
                expected[i8 + 1] = rgba64Bytes[1];
                expected[i8 + 2] = rgba64Bytes[2];
                expected[i8 + 3] = rgba64Bytes[3];
                expected[i8 + 4] = rgba64Bytes[4];
                expected[i8 + 5] = rgba64Bytes[5];
                expected[i8 + 6] = rgba64Bytes[6];
                expected[i8 + 7] = rgba64Bytes[7];
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToRgba64Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromBgr24Bytes(int count)
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
                (s, d) => Operations.PackFromBgr24Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToBgr24Bytes(int count)
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
                (s, d) => Operations.ToBgr24Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromBgra32Bytes(int count)
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
                (s, d) => Operations.PackFromBgra32Bytes(s, d.GetSpan(), count)
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
                (s, d) => Operations.ToBgra32Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromArgb32Bytes(int count)
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
                (s, d) => Operations.PackFromArgb32Bytes(s, d.GetSpan(), count)
            );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToArgb32Bytes(int count)
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
                (s, d) => Operations.ToArgb32Bytes(s, d.GetSpan(), count)
            );
        }

        private class TestBuffers<TSource, TDest> : IDisposable
            where TSource : struct
            where TDest : struct
        {
            public TSource[] SourceBuffer { get; }
            public IMemoryOwner<TDest> ActualDestBuffer { get; }
            public TDest[] ExpectedDestBuffer { get; }

            public TestBuffers(TSource[] source, TDest[] expectedDest)
            {
                this.SourceBuffer = source;
                this.ExpectedDestBuffer = expectedDest;
                this.ActualDestBuffer = Configuration.Default.MemoryAllocator.Allocate<TDest>(expectedDest.Length);
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
                    Span<Vector4> actual = MemoryMarshal.Cast<TDest, Vector4>(this.ActualDestBuffer.GetSpan());

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
                    Span<TDest> actual = this.ActualDestBuffer.GetSpan();
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
            Action<TSource[], IMemoryOwner<TDest>> action)
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

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct Rgba64Bytes
        {
            public fixed byte Data[8];

            public byte this[int idx]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    ref byte self = ref Unsafe.As<Rgba64Bytes, byte>(ref this);
                    return Unsafe.Add(ref self, idx);
                }
            }
        }
    }
}