// <copyright file="WithFileAttribute.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Triggers passing <see cref="TestImageFactory{TColor}"/> instances which read an image from the given file
    /// One <see cref="TestImageFactory{TColor}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
    /// </summary>
    public class WithFileAttribute : ImageDataAttributeBase
    {
        private readonly string fileName;

        /// <summary>
        /// Triggers passing <see cref="TestImageFactory{TColor}"/> instances which read an image from the given file
        /// One <see cref="TestImageFactory{TColor}"/> instance will be passed for each the pixel format defined by the pixelTypes parameter
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <param name="pixelTypes">The requested pixel types</param>
        /// <param name="additionalParameters">Additional theory parameter values</param>
        public WithFileAttribute(string fileName, PixelTypes pixelTypes, params object[] additionalParameters)
            : base(pixelTypes, additionalParameters)
        {
            this.fileName = fileName;
        }

        protected override object[] GetFactoryMethodArgs(MethodInfo testMethod, Type factoryType) => new object[] { this.fileName };

        protected override string GetFactoryMethodName(MethodInfo testMethod) => "File";
    }
}