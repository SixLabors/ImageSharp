namespace ImageSharp.Tests.Colors
{
    using System;
    using System.Numerics;

    using Xunit;

    public class BulkPixelOperationsTests
    {
        public class Color : BulkPixelOperationsTests<ImageSharp.Color>
        {
            // For 4.6 test runner MemberData does not work without redeclaring the public field in the derived test class:
            public static TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };

            [Fact]
            public void IsSpecialImplementation()
            {
                Assert.IsType<ImageSharp.Color.BulkOperations>(BulkPixelOperations<ImageSharp.Color>.Instance);
            }
            
            [Fact]
            public void ToVector4SimdAligned()
            {
                ImageSharp.Color[] source = CreatePixelTestData(64);
                Vector4[] expected = CreateExpectedVector4Data(source);

                TestOperation(
                    source,
                    expected,
                    (s, d) => ImageSharp.Color.BulkOperations.ToVector4SimdAligned(s, d, 64)
                    );
            }
        }

        public class Argb : BulkPixelOperationsTests<ImageSharp.Argb>
        {
            // For 4.6 test runner MemberData does not work without redeclaring the public field in the derived test class:
            public static TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.All)]
        public void GetGlobalInstance<TColor>(TestImageProvider<TColor> dummy)
            where TColor:struct, IPixel<TColor>
        {
            Assert.NotNull(BulkPixelOperations<TColor>.Instance);
        }
    }

    public abstract class BulkPixelOperationsTests<TColor>
        where TColor : struct, IPixel<TColor>
    {
        public static TheoryData<int> ArraySizesData => new TheoryData<int> { 7, 16, 1111 };

        private static BulkPixelOperations<TColor> Operations => BulkPixelOperations<TColor>.Instance;

        internal static TColor[] CreateExpectedPixelData(Vector4[] source)
        {
            TColor[] expected = new TColor[source.Length];

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
            TColor[] expected = CreateExpectedPixelData(source);

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromVector4(s, d, count)
                );
        }

        internal static Vector4[] CreateExpectedVector4Data(TColor[] source)
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
            TColor[] source = CreatePixelTestData(count);
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
            TColor[] expected = new TColor[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].PackFromBytes(source[i3 + 0], source[i3 + 1], source[i3 + 2], 255);
            }

            TestOperation(
                source, 
                expected, 
                (s, d) => Operations.PackFromXyzBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToXyzBytes(int count)
        {
            TColor[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 3];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                source[i].ToXyzBytes(expected, i3);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToXyzBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromXyzwBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            TColor[] expected = new TColor[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromBytes(source[i4 + 0], source[i4 + 1], source[i4 + 2], source[i4 + 3]);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromXyzwBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToXyzwBytes(int count)
        {
            TColor[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToXyzwBytes(expected, i4);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToXyzwBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 3);
            TColor[] expected = new TColor[count];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;

                expected[i].PackFromBytes(source[i3 + 2], source[i3 + 1], source[i3 + 0], 255);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromZyxBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToZyxBytes(int count)
        {
            TColor[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 3];

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                source[i].ToZyxBytes(expected, i3);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToZyxBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxwBytes(int count)
        {
            byte[] source = CreateByteTestData(count * 4);
            TColor[] expected = new TColor[count];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;

                expected[i].PackFromBytes(source[i4 + 2], source[i4 + 1], source[i4 + 0], source[i4 + 3]);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.PackFromZyxwBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void ToZyxwBytes(int count)
        {
            TColor[] source = CreatePixelTestData(count);
            byte[] expected = new byte[count * 4];

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                source[i].ToZyxwBytes(expected, i4);
            }

            TestOperation(
                source,
                expected,
                (s, d) => Operations.ToZyxwBytes(s, d, count)
                );
        }

        
        private class TestBuffers<TSource, TDest> : IDisposable
            where TSource : struct
            where TDest : struct
        {
            public PinnedBuffer<TSource> SourceBuffer { get; }
            public PinnedBuffer<TDest> ActualDestBuffer { get; }
            public PinnedBuffer<TDest> ExpectedDestBuffer { get; }

            public BufferPointer<TSource> Source => this.SourceBuffer.Slice();
            public BufferPointer<TDest> ActualDest => this.ActualDestBuffer.Slice();
            
            public TestBuffers(TSource[] source, TDest[] expectedDest)
            {
                this.SourceBuffer = new PinnedBuffer<TSource>(source);
                this.ExpectedDestBuffer = new PinnedBuffer<TDest>(expectedDest);
                this.ActualDestBuffer = new PinnedBuffer<TDest>(expectedDest.Length);
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
                int count = this.ExpectedDestBuffer.Count;

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
            Action<BufferPointer<TSource>, BufferPointer<TDest>> action)
            where TSource : struct
            where TDest : struct
        {
            using (var buffers = new TestBuffers<TSource, TDest>(source, expected))
            {
                action(buffers.Source, buffers.ActualDest);
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

        internal static TColor[] CreatePixelTestData(int length)
        {
            TColor[] result = new TColor[length];

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