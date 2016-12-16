namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;

    /// <summary>
    /// Wrapper class to erase the generic parameters of <see cref="Image{TColor, TPacked}"/> 
    /// to support parametric testing with <see cref="System.Type"/> instances for pixel color types
    /// </summary>
    public abstract class ImageTester
    {
        protected ImageTester(IImageBase image)
        {
            this.Image = image;
        }

        public IImageBase Image { get; }

        public abstract Vector4 GetPixelAsVector4(int x, int y);

        public abstract Vector4 SetPixelFromVector4(int x, int y, Vector4 value);

        private class Implementation<TColor, TPacked> : ImageTester
            where TPacked : struct
            where TColor : struct, IPackedPixel<TPacked>
        {
            public Implementation(int width, int height)
                : base(new Image<TColor, TPacked>(width, height))
            {
            }

            public Implementation(Stream stream)
                : base(new Image<TColor, TPacked>(stream))
            {
            }

            public override Vector4 GetPixelAsVector4(int x, int y)
            {
                throw new NotImplementedException();
            }

            public override Vector4 SetPixelFromVector4(int x, int y, Vector4 value)
            {
                throw new NotImplementedException();
            }
        }

        public static ImageTester Create(Type pixelType, int width, int height)
        {
            var implType = MakeImplType(pixelType);
            return (ImageTester)Activator.CreateInstance(implType, width, height);
        }

        public static ImageTester Create(Type pixelType, string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Create(pixelType, stream);
            }
        }

        public static ImageTester Create(Type pixelType, Stream stream)
        {
            var implType = MakeImplType(pixelType);
            return (ImageTester)Activator.CreateInstance(implType, stream);
        }

        private static Type MakeImplType(Type pixelType)
        {
            Type packedType = GetPackedType(pixelType);
            var implType = typeof(Implementation<,>).MakeGenericType(pixelType, packedType);
            return implType;
        }

        internal static Type GetPackedType(Type pixelType)
        {
            var intrfcType =
                pixelType.GetInterfaces()
                    .Single(i => i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(IPackedPixel<>));

            return intrfcType.GetGenericArguments().Single();
        }
    }
}