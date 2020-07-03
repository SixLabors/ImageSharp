// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;

namespace SixLabors.ImageSharp.Tests
{
    public class WithBasicTestPatternImagesAttribute : ImageDataAttributeBase
    {
        public WithBasicTestPatternImagesAttribute(int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
            : this(null, width, height, pixelTypes, additionalParameters)
        {
        }

        public WithBasicTestPatternImagesAttribute(string memberData, int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(memberData, pixelTypes, additionalParameters)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Gets the width
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height
        /// </summary>
        public int Height { get; }

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "BasicTestPattern";

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType) => new object[] { this.Width, this.Height };
    }
}