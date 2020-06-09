// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce a blank image of size width * height.
    /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithBlankImagesAttribute : ImageDataAttributeBase
    {
        /// <summary>
        /// Triggers passing an <see cref="TestImageProvider{TPixel}"/> that produces a blank image of size width * height
        /// </summary>
        /// <param name="width">The required width</param>
        /// <param name="height">The required height</param>
        /// <param name="pixelTypes">The requested parameter</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithBlankImagesAttribute(int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(null, pixelTypes, additionalParameters)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Triggers passing an <see cref="TestImageProvider{TPixel}"/> that produces a blank image of size width * height
        /// </summary>
        /// <param name="memberData">The member data</param>
        /// <param name="width">The required width</param>
        /// <param name="height">The required height</param>
        /// <param name="pixelTypes">The requested parameter</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithBlankImagesAttribute(string memberData, int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(memberData, pixelTypes, additionalParameters)
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
