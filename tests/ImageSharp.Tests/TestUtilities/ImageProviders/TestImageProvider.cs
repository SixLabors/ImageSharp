// <copyright file="TestImageProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Reflection;

    using ImageSharp.PixelFormats;

    using Xunit.Abstractions;

    /// <summary>
    /// Provides <see cref="Image{TPixel}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TPixel> 
        where TPixel : struct, IPixel<TPixel>
    {
        public PixelTypes PixelType { get; private set; } = typeof(TPixel).GetPixelType();

        public virtual string SourceFileOrDescription => "";

        /// <summary>
        /// Utility instance to provide informations about the test image & manage input/output
        /// </summary>
        public ImagingTestCaseUtility Utility { get; private set; }

        public GenericFactory<TPixel> Factory { get; private set; } = new GenericFactory<TPixel>();
        public string TypeName { get; private set; }
        public string MethodName { get; private set; }

        public static TestImageProvider<TPixel> TestPattern(
                int width,
                int height,
                MethodInfo testMethod = null,
                PixelTypes pixelTypeOverride = PixelTypes.Undefined)
            => new TestPatternProvider(width, height).Init(testMethod, pixelTypeOverride);

        public static TestImageProvider<TPixel> Blank(
                        int width,
                        int height,
                        MethodInfo testMethod = null,
                        PixelTypes pixelTypeOverride = PixelTypes.Undefined)
                    => new BlankProvider(width, height).Init(testMethod, pixelTypeOverride);

        public static TestImageProvider<TPixel> File(
            string filePath,
            MethodInfo testMethod = null,
            PixelTypes pixelTypeOverride = PixelTypes.Undefined)
        {
            return new FileProvider(filePath).Init(testMethod, pixelTypeOverride);
        }

        public static TestImageProvider<TPixel> Lambda(
                Func<GenericFactory<TPixel>, Image<TPixel>> func,
                MethodInfo testMethod = null,
                PixelTypes pixelTypeOverride = PixelTypes.Undefined)
            => new LambdaProvider(func).Init(testMethod, pixelTypeOverride);

        public static TestImageProvider<TPixel> Solid(
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
        /// Returns an <see cref="Image{TPixel}"/> instance to the test case with the necessary traits.
        /// </summary>
        public abstract Image<TPixel> GetImage();

        public virtual void Deserialize(IXunitSerializationInfo info)
        {
            PixelTypes pixelType = info.GetValue<PixelTypes>("PixelType");
            string typeName = info.GetValue<string>("TypeName");
            string methodName = info.GetValue<string>("MethodName");

            this.Init(typeName, methodName, pixelType);
        }

        public virtual void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("PixelType", this.PixelType);
            info.AddValue("TypeName", this.TypeName);
            info.AddValue("MethodName", this.MethodName);
        }

        protected TestImageProvider<TPixel> Init(string typeName, string methodName, PixelTypes pixelTypeOverride)
        {
            if (pixelTypeOverride != PixelTypes.Undefined)
            {
                this.PixelType = pixelTypeOverride;
            }
            this.TypeName = typeName;
            this.MethodName = methodName;

            if (pixelTypeOverride == PixelTypes.StandardImageClass)
            {
                this.Factory = new ImageFactory() as GenericFactory<TPixel>;
            }

            this.Utility = new ImagingTestCaseUtility()
            {
                SourceFileOrDescription = this.SourceFileOrDescription,
                PixelTypeName = this.PixelType.ToString()
            };

            if (methodName != null)
            {
                this.Utility.Init(typeName, methodName);
            }

            return this;
        }

        protected TestImageProvider<TPixel> Init(MethodInfo testMethod, PixelTypes pixelTypeOverride)
        {
            return Init(testMethod?.DeclaringType.Name, testMethod?.Name, pixelTypeOverride);
        }

        public override string ToString()
        {
            string provName = this.GetType().Name.Replace("Provider", "");
            return $"{this.SourceFileOrDescription}[{this.PixelType}]";
        }
    }
}