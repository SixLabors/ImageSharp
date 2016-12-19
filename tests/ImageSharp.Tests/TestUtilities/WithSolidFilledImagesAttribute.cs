// <copyright file="WithSolidFilledImagesAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Triggers passing <see cref="TestImageFactory{TColor,TPacked}"/> instances which produce an image of size width * height filled with the requested color.
    /// One <see cref="TestImageFactory{TColor,TPacked}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithSolidFilledImagesAttribute : WithBlankImagesAttribute
    {
        /// <summary>
        /// Triggers passing <see cref="TestImageFactory{TColor,TPacked}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageFactory{TColor,TPacked}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="width">The width of the requested image</param>
        /// <param name="height">The height of the requested image</param>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
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

        /// <summary>
        /// Triggers passing <see cref="TestImageFactory{TColor,TPacked}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageFactory{TColor,TPacked}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="width">The width of the requested image</param>
        /// <param name="height">The height of the requested image</param>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// /// <param name="a">Alpha</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
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
        
        /// <summary>
        /// Red
        /// </summary>
        public byte R { get; }

        /// <summary>
        /// Green
        /// </summary>
        public byte G { get; }

        /// <summary>
        /// Blue
        /// </summary>
        public byte B { get; }

        /// <summary>
        /// Alpha
        /// </summary>
        public byte A { get; }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
            => new object[] { this.Width, this.Height, this.R, this.G, this.B, this.A };

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Solid";
    }
}