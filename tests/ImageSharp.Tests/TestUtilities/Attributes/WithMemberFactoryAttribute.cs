// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Reflection;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which return the image produced by the given test class member method
    /// <see cref="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
    /// The parameter of the factory method must be a <see cref="GenericFactory{TPixel}"/> instance
    /// </summary>
    public class WithMemberFactoryAttribute : ImageDataAttributeBase
    {
        private readonly string memberMethodName;

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which return the image produced by the given test class member method
        /// <see cref="TestImageProvider{TPixel}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="memberMethodName">The name of the static test class which returns the image</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithMemberFactoryAttribute(string memberMethodName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(null, pixelTypes, additionalParameters)
        {
            this.memberMethodName = memberMethodName;
        }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            MethodInfo m = testMethod.DeclaringType.GetMethod(this.memberMethodName);

            Type[] args = factoryType.GetGenericArguments();
            Type colorType = args.Single();

            Type imgType = typeof(Image<>).MakeGenericType(colorType);

            Type funcType = typeof(Func<>).MakeGenericType(imgType);

            MethodInfo genericMethod = m.MakeGenericMethod(args);

            Delegate d = genericMethod.CreateDelegate(funcType);
            return new object[] { d };
        }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Lambda";
    }
}