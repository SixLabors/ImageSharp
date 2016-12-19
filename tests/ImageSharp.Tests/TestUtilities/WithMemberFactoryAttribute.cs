// <copyright file="WithMemberFactoryAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    public class WithMemberFactoryAttribute : ImageDataAttributeBase
    {
        private readonly string memberMethodName;

        public WithMemberFactoryAttribute(string memberMethodName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(pixelTypes, additionalParameters)
        {
            this.memberMethodName = memberMethodName;
        }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
        {
            var m = testMethod.DeclaringType.GetMethod(this.memberMethodName);

            var args = factoryType.GetGenericArguments();
            var imgType = typeof(Image<,>).MakeGenericType(args);
            var funcType = typeof(Func<>).MakeGenericType(imgType);

            var genericMethod = m.MakeGenericMethod(args);

            var d = genericMethod.CreateDelegate(funcType);
            return new object[] { d };
        }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Lambda";
    }
}