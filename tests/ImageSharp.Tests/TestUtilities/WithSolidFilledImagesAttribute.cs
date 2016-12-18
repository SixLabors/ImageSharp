namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    public class WithSolidFilledImagesAttribute : WithBlankImagesAttribute
    {
        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public byte A { get; }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Solid";

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
            => new object[] { this.Width, this.Height, this.R, this.G, this.B, this.A };


        public WithSolidFilledImagesAttribute(
            int width,
            int height,
            byte r,
            byte g,
            byte b,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : this(width, height, r, g, b, 255, pixelTypes, additionalParameters)
        {
        }

        public WithSolidFilledImagesAttribute(
            int width,
            int height,
            byte r,
            byte g,
            byte b,
            byte a,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(width, height, pixelTypes, additionalParameters)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }
    }
}