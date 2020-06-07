// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.PixelFormats;

    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce an image of size width * height filled with the requested color.
    /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithSolidFilledImagesAttribute : WithBlankImagesAttribute
    {
        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
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
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
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
            : this(null, width, height, r, g, b, a, pixelTypes, additionalParameters)
        {
        }

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="memberData">The member data to apply to theories</param>
        /// <param name="width">The width of the requested image</param>
        /// <param name="height">The height of the requested image</param>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        /// /// <param name="a">Alpha</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithSolidFilledImagesAttribute(
            string memberData,
            int width,
            int height,
            byte r,
            byte g,
            byte b,
            byte a,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(memberData, width, height, pixelTypes, additionalParameters)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="width">The width of the requested image</param>
        /// <param name="height">The height of the requested image</param>
        /// <param name="colorName">The referenced color name (name of property in <see cref="Color"/>).</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithSolidFilledImagesAttribute(
            int width,
            int height,
            string colorName,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : this(null, width, height, colorName, pixelTypes, additionalParameters)
        {
        }

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which produce an image of size width * height filled with the requested color.
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="memberData">The member data to apply to theories</param>
        /// <param name="width">The width of the requested image</param>
        /// <param name="height">The height of the requested image</param>
        /// <param name="colorName">The referenced color name (name of property in <see cref="Color"/>).</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithSolidFilledImagesAttribute(
            string memberData,
            int width,
            int height,
            string colorName,
            PixelTypes pixelTypes,
            params object[] additionalParameters)
            : base(memberData, width, height, pixelTypes, additionalParameters)
        {
            Guard.NotNull(colorName, nameof(colorName));

            Rgba32 c = TestUtils.GetPixelOfNamedColor<Rgba32>(colorName);
            this.R = c.R;
            this.G = c.G;
            this.B = c.B;
            this.A = c.A;
        }

        /// <summary>
        /// Gets the red component.
        /// </summary>
        public byte R { get; }

        /// <summary>
        /// Gets the green component.
        /// </summary>
        public byte G { get; }

        /// <summary>
        /// Gets the blue component.
        /// </summary>
        public byte B { get; }

        /// <summary>
        /// Gets the alpha component.
        /// </summary>
        public byte A { get; }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType)
            => new object[] { this.Width, this.Height, this.R, this.G, this.B, this.A };

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "Solid";
    }
}
