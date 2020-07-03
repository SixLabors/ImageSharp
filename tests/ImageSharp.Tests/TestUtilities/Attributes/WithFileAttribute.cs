// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image from the given file
    /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithFileAttribute : ImageDataAttributeBase
    {
        private readonly string fileName;

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image from the given file
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileAttribute(string fileName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(null, pixelTypes, additionalParameters)
        {
            this.fileName = fileName;
        }

        /// <summary>
        /// Triggers passing <see cref="TestImageProvider{TPixel}"/> instances which read an image from the given file
        /// One <see cref="TestImageProvider{TPixel}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileAttribute(string fileName, string dataMemberName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(dataMemberName, pixelTypes, additionalParameters)
        {
            this.fileName = fileName;
        }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType) => new object[] { this.fileName };

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "File";
    }
}