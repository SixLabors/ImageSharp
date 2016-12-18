namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    public class WithBlankImagesAttribute : ImageDataAttributeBase
    {
        public WithBlankImagesAttribute(int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(pixelTypes, additionalParameters)
        {
            this.Width = width;
            this.Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Blank";

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType) => new object[] { this.Width, this.Height };
    }
}