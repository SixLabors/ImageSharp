// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Reflection;
using Castle.Core.Internal;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public interface ITestImageProvider
    {
        PixelTypes PixelType { get; }
        ImagingTestCaseUtility Utility { get; }
        string SourceFileOrDescription { get; }

        Configuration Configuration { get; set; }
    }

    /// <summary>
    /// Provides <see cref="Image{TPixel}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TPixel> : ITestImageProvider
        where TPixel : struct, IPixel<TPixel>
    {
        public PixelTypes PixelType { get; private set; } = typeof(TPixel).GetPixelType();

        public virtual string SourceFileOrDescription => "";

        public Configuration Configuration { get; set; } = Configuration.Default.Clone();

        /// <summary>
        /// Utility instance to provide informations about the test image & manage input/output
        /// </summary>
        public ImagingTestCaseUtility Utility { get; private set; }

        public string TypeName { get; private set; }
        public string MethodName { get; private set; }
        public string OutputSubfolderName { get; private set; }

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
                Func<Image<TPixel>> factoryFunc,
                MethodInfo testMethod = null,
                PixelTypes pixelTypeOverride = PixelTypes.Undefined)
            => new LambdaProvider(factoryFunc).Init(testMethod, pixelTypeOverride);

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

        public virtual Image<TPixel> GetImage(IImageDecoder decoder)
        {
            throw new NotSupportedException($"Decoder specific GetImage() is not supported with {this.GetType().Name}!");
        }

        /// <summary>
        /// Returns an <see cref="Image{TPixel}"/> instance to the test case with the necessary traits.
        /// </summary>
        public Image<TPixel> GetImage(Action<IImageProcessingContext<TPixel>> operationsToApply)
        {
            Image<TPixel> img = GetImage();
            img.Mutate(operationsToApply);
            return img;
        }

        public virtual void Deserialize(IXunitSerializationInfo info)
        {
            PixelTypes pixelType = info.GetValue<PixelTypes>("PixelType");
            string typeName = info.GetValue<string>("TypeName");
            string methodName = info.GetValue<string>("MethodName");
            string outputSubfolderName = info.GetValue<string>("OutputSubfolderName");

            this.Init(typeName, methodName, outputSubfolderName, pixelType);
        }

        public virtual void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("PixelType", this.PixelType);
            info.AddValue("TypeName", this.TypeName);
            info.AddValue("MethodName", this.MethodName);
            info.AddValue("OutputSubfolderName", this.OutputSubfolderName);
        }

        protected TestImageProvider<TPixel> Init(
            string typeName,
            string methodName,
            string outputSubfolerName,
            PixelTypes pixelTypeOverride)
        {
            if (pixelTypeOverride != PixelTypes.Undefined)
            {
                this.PixelType = pixelTypeOverride;
            }
            this.TypeName = typeName;
            this.MethodName = methodName;
            this.OutputSubfolderName = outputSubfolerName;

            this.Utility = new ImagingTestCaseUtility
            {
                SourceFileOrDescription = this.SourceFileOrDescription,
                PixelTypeName = this.PixelType.ToString()
            };

            if (methodName != null)
            {
                this.Utility.Init(typeName, methodName, outputSubfolerName);
            }

            return this;
        }

        protected TestImageProvider<TPixel> Init(MethodInfo testMethod, PixelTypes pixelTypeOverride)
        {
            string subfolder = testMethod?.DeclaringType.GetAttribute<GroupOutputAttribute>()?.Subfolder
                               ?? string.Empty;
            return this.Init(testMethod?.DeclaringType.Name, testMethod?.Name, subfolder, pixelTypeOverride);
        }

        public override string ToString()
        {
            string provName = this.GetType().Name.Replace("Provider", "");
            return $"{this.SourceFileOrDescription}[{this.PixelType}]";
        }
    }
}