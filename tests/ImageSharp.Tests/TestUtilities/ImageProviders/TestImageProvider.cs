// <copyright file="TestImageProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Provides <see cref="Image{TColor}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TColor">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        public PixelTypes PixelType { get; private set; } = typeof(TColor).GetPixelType();

        public virtual string SourceFileOrDescription => "";

        /// <summary>
        /// Utility instance to provide informations about the test image & manage input/output
        /// </summary>
        public ImagingTestCaseUtility Utility { get; private set; }

        public GenericFactory<TColor> Factory { get; private set; } = new GenericFactory<TColor>();

        public static TestImageProvider<TColor> Blank(
                int width,
                int height,
                MethodInfo testMethod = null,
                PixelTypes pixelTypeOverride = PixelTypes.Undefined)
            => new BlankProvider(width, height).Init(testMethod, pixelTypeOverride);

        public static TestImageProvider<TColor> File(
            string filePath,
            MethodInfo testMethod = null,
            PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        {
            return new FileProvider(filePath).Init(testMethod, pixelTypeOverride);
        }

        public static TestImageProvider<TColor> Lambda(
                Func<GenericFactory<TColor>, Image<TColor>> func,
                MethodInfo testMethod = null,
                PixelTypes pixelTypeOverride = PixelTypes.Undefined)
            => new LambdaProvider(func).Init(testMethod, pixelTypeOverride);

        public static TestImageProvider<TColor> Solid(
            int width,
            int height,
            byte r,
            byte g,
            byte b,
            byte a = 255,
            MethodInfo testMethod = null,
            PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        {
            return new SolidProvider(width, height, r, g, b, a).Init(testMethod, pixelTypeOverride);
        }

        /// <summary>
        /// Returns an <see cref="Image{TColor}"/> instance to the test case with the necessary traits.
        /// </summary>
        public abstract Image<TColor> GetImage();

        protected TestImageProvider<TColor> Init(MethodInfo testMethod, PixelTypes pixelTypeOverride)
        {
            if (pixelTypeOverride != PixelTypes.Undefined)
            {
                this.PixelType = pixelTypeOverride;
            }

            if (pixelTypeOverride == PixelTypes.StandardImageClass)
            {
                this.Factory = new ImageFactory() as GenericFactory<TColor>;
            }

            this.Utility = new ImagingTestCaseUtility()
                               {
                                   SourceFileOrDescription = this.SourceFileOrDescription,
                                   PixelTypeName = this.PixelType.ToString()
                               };

            if (testMethod != null)
            {
                this.Utility.Init(testMethod);
            }

            return this;
        }

        public override string ToString()
        {
            string provName = this.GetType().Name.Replace("Provider", "");
            return $"{this.SourceFileOrDescription}[{this.PixelType}]";
        }
    }
}