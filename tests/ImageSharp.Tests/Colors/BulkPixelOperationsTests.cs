namespace ImageSharp.Tests.Colors
{
    using System;
    using System.Numerics;

    using Xunit;
    
    public abstract class BulkPixelOperationsTests<TColor>
        where TColor : struct, IPixel<TColor>
    {
        public class ColorPixels : BulkPixelOperationsTests<Color>
        {
        }

        public class ArgbPixels : BulkPixelOperationsTests<Argb>
        {
        }

        public static TheoryData<int> ArraySizesData = new TheoryData<int> { 7, 16, 1111 };
        
        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackFromVector4(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackToVector4(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackToXyzBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackFromXyzBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackToXyzwBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackFromXyzwBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackToZyxBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackFromZyxBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackToZyxwBytes(int count)
        {
            throw new NotImplementedException();
        }

        [Theory]
        [MemberData(nameof(ArraySizesData))]
        public virtual void PackFromZyxwBytes(int count)
        {
            throw new NotImplementedException();
        }

        public class TestBuffers
        {
            internal static PinnedBuffer<Vector4> Vector4(int length)
            {
                Vector4[] result = new Vector4[length];
                Random rnd = new Random(42); // Deterministic random values

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = GetVector(rnd);
                }

                return new PinnedBuffer<Vector4>(result);
            }

            internal static PinnedBuffer<TColor> Pixel(int length)
            {
                TColor[] result = new TColor[length];

                Random rnd = new Random(42); // Deterministic random values

                for (int i = 0; i < result.Length; i++)
                {
                    Vector4 v = GetVector(rnd);
                    result[i].PackFromVector4(v);
                }

                return new PinnedBuffer<TColor>(result);
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
}