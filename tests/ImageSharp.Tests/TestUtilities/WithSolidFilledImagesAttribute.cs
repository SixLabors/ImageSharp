// <copyright file="WithSolidFilledImagesAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    public class WithSolidFilledImagesAttribute : WithBlankImagesAttribute
    {
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

        public byte A { get; }

        public byte B { get; }

        public byte G { get; }

        public byte R { get; }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
            => new object[] { this.Width, this.Height, this.R, this.G, this.B, this.A };

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Solid";
    }
}