// <copyright file="WithBlankImagesAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

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