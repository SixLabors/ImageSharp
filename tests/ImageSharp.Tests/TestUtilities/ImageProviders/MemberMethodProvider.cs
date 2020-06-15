// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Provides <see cref="Image{TPixel}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TPixel> : IXunitSerializable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private class MemberMethodProvider : TestImageProvider<TPixel>
        {
            private string declaringTypeName;
            private string methodName;
            private Func<Image<TPixel>> factoryFunc;

            public MemberMethodProvider()
            {
            }

            public MemberMethodProvider(string declaringTypeName, string methodName)
            {
                this.declaringTypeName = declaringTypeName;
                this.methodName = methodName;
            }

            public override Image<TPixel> GetImage()
            {
                this.factoryFunc ??= this.GetFactory();
                return this.factoryFunc();
            }

            public override void Serialize(IXunitSerializationInfo info)
            {
                base.Serialize(info);

                info.AddValue(nameof(this.declaringTypeName), this.declaringTypeName);
                info.AddValue(nameof(this.methodName), this.methodName);
            }

            public override void Deserialize(IXunitSerializationInfo info)
            {
                base.Deserialize(info);

                this.methodName = info.GetValue<string>(nameof(this.methodName));
                this.declaringTypeName = info.GetValue<string>(nameof(this.declaringTypeName));
            }

            private Func<Image<TPixel>> GetFactory()
            {
                var declaringType = Type.GetType(this.declaringTypeName);
                MethodInfo m = declaringType.GetMethod(this.methodName);
                Type pixelType = typeof(TPixel);
                Type imgType = typeof(Image<>).MakeGenericType(pixelType);
                Type funcType = typeof(Func<>).MakeGenericType(imgType);
                MethodInfo genericMethod = m.MakeGenericMethod(pixelType);
                return (Func<Image<TPixel>>)genericMethod.CreateDelegate(funcType);
            }
        }
    }
}
