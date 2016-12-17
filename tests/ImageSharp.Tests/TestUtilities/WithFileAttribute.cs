namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    public class WithFileAttribute : ImageDataAttributeBase
    {
        private readonly string fileName;

        public WithFileAttribute(string fileName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(pixelTypes, additionalParameters)
        {
            this.fileName = fileName;
        }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType) => new object[] { this.fileName };

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "File";
    }
}