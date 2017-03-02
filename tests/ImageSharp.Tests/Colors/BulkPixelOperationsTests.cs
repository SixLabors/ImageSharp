namespace ImageSharp.Tests.Colors
{
    using System;

    using Xunit;

    public class BulkPixelOperationsTests
    {
        public class TypeParam<TColor>
        {
        }


        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackFromVector4<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackToVector4<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackToXyzBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackFromXyzBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackToXyzwBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackFromXyzwBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackToZyxBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackFromZyxBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackToZyxwBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }

        [Theory]
        [InlineData(default(TypeParam<Color>))]
        [InlineData(default(TypeParam<Argb>))]
        public virtual void PackFromZyxwBytes<TColor>(TypeParam<TColor> dummy)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }
    }
}