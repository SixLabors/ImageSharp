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
    public class WithTestPatternImagesAttribute : ImageDataAttributeBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WithTestPatternImagesAttribute"/> class.
        /// </summary>
        /// <param name="width">The required width</param>
        /// <param name="height">The required height</param>
        /// <param name="pixelTypes">The requested parameter</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithTestPatternImagesAttribute(int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
            : this(null, width, height, pixelTypes, additionalParameters)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WithTestPatternImagesAttribute"/> class.
        /// </summary>
        /// <param name="memberData">The member data to apply to theories</param>
        /// <param name="width">The required width</param>
        /// <param name="height">The required height</param>
        /// <param name="pixelTypes">The requested parameter</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithTestPatternImagesAttribute(string memberData, int width, int height, PixelTypes pixelTypes, params object[] additionalParameters)
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

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "TestPattern";

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType) => new object[] { this.Width, this.Height };
    }
}
