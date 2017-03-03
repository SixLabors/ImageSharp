namespace ImageSharp.Tests.Colors
{
    using System;
    using System.Numerics;

    using Xunit;

    public abstract class BulkPixelOperationsTests
    {
        public class ColorPixels : BulkPixelOperationsTests<Color>
        {
        }

        public class ArgbPixels : BulkPixelOperationsTests<Argb>
        {
        }
    }

    public abstract class BulkPixelOperationsTests<TColor>
        where TColor : struct, IPixel<TColor>
    {
        public static TheoryData<int> ArraySizesData = new TheoryData<int> { 7, 16, 1111 };
        
        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromVector4(int count)
        {
            Vector4[] source = CreateVector4TestData(count);
            TColor[] expected = new TColor[count];

            for (int i = 0; i < count; i++)
            {
                expected[i].PackFromVector4(source[i]);
            }

            TestOperation(
                source,
                expected,
                (ops, s, d) => ops.PackFromVector4(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackToVector4(int count)
        {
            TColor[] source = CreatePixelTestData(count);
            Vector4[] expected = new Vector4[count];

            for (int i = 0; i < count; i++)
            {
                expected[i] = source[i].ToVector4();
            }

            TestOperation(
                source,
                expected,
                (ops, s, d) => ops.PackToVector4(s, d, count)
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
                (ops, s, d) => ops.PackFromXyzBytes(s, d, count)
                );
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackToXyzBytes(int count)
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
                (ops, s, d) => ops.PackToXyzBytes(s, d, count)
                );
        }


        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackToXyzwBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromXyzwBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackToZyxBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackToZyxwBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public void PackFromZyxwBytes(int count)
        {
            throw new NotImplementedException();
        }
        
        private class TestBuffers<TSource, TDest> : IDisposable
            where TSource : struct
            where TDest : struct
        {
            public PinnedBuffer<TSource> SourceBuffer { get; }
            public PinnedBuffer<TDest> ActualDestBuffer { get; }
            public PinnedBuffer<TDest> ExpectedDestBuffer { get; }

            public ArrayPointer<TSource> Source => this.SourceBuffer.GetArrayPointer();
            public ArrayPointer<TDest> ActualDest => this.ActualDestBuffer.GetArrayPointer();
            
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

            public void Verify()
            {
                int count = this.ExpectedDestBuffer.Count;
                TDest[] expected = this.ExpectedDestBuffer.Array;
                TDest[] actual = this.ActualDestBuffer.Array;
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(expected[i], actual[i]);
                }
            }
        }

        private static void TestOperation<TSource, TDest>(
            TSource[] source,
            TDest[] expected,
            Action<BulkPixelOperations<TColor>, ArrayPointer<TSource>, ArrayPointer<TDest>> action)
            where TSource : struct
            where TDest : struct
        {
            using (var buffers = new TestBuffers<TSource, TDest>(source, expected))
            {
                action(BulkPixelOperations<TColor>.Instance, buffers.Source, buffers.ActualDest);
                buffers.Verify();
            }
        }

        private static Vector4[] CreateVector4TestData(int length)
        {
            Vector4[] result = new Vector4[length];
            Random rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = GetVector(rnd);
            }
            return result;
        }

        private static TColor[] CreatePixelTestData(int length)
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

        private static byte[] CreateByteTestData(int length)
        {
            byte[] result = new byte[length];
            Random rnd = new Random(42); // Deterministic random values

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (byte)rnd.Next(255);
            }
            return result;
        }
        
        private static Vector4 GetVector(Random rnd)
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