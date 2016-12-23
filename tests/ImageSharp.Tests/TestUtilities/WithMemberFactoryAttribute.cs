// <copyright file="WithMemberFactoryAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TColor}"/> instances which return the image produced by the given test class member method
    /// <see cref="TestImageProvider{TColor}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithMemberFactoryAttribute : ImageDataAttributeBase
    {
        private readonly string memberMethodName;

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TColor}"/> instances which return the image produced by the given test class member method
        /// <see cref="TestImageProvider{TColor}"/> instances will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="memberMethodName">The name of the static test class which returns the image</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithMemberFactoryAttribute(string memberMethodName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(pixelTypes, additionalParameters)
        {
            this.memberMethodName = memberMethodName;
        }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            var m = testMethod.DeclaringType.GetMethod(this.memberMethodName);

            var args = factoryType.GetGenericArguments();
            var imgType = typeof(Image<>).MakeGenericType(args);
            var funcType = typeof(Func<>).MakeGenericType(imgType);

            var genericMethod = m.MakeGenericMethod(args);

            var d = genericMethod.CreateDelegate(funcType);
            return new object[] { d };
        }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Lambda";
    }
}