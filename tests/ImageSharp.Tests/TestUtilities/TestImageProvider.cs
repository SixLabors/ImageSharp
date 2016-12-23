// <copyright file="TestImageProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;

    /// <summary>
    /// Provides <see cref="Image{TColor}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TColor">The pixel format of the image</typeparam>
    public abstract class TestImageProvider<TColor> : ITestImageFactory
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

            if (pixelTypeOverride == PixelTypes.ColorWithDefaultImageClass)
            {
                this.Factory = new DefaultImageClassSpecificFactory() as GenericFactory<TColor>;
            }


            this.Utility = new ImagingTestCaseUtility()
                               {
                                   SourceFileOrDescription = this.SourceFileOrDescription,
                                   PixelTypeName = typeof(TColor).Name
                               };

            if (testMethod != null)
            {
                this.Utility.Init(testMethod);
            }

            return this;
        }

        private class BlankProvider : TestImageProvider<TColor>
        {
            public BlankProvider(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }

            public override string SourceFileOrDescription => $"Blank{this.Width}x{this.Height}";

            protected int Height { get; }

            protected int Width { get; }

            public override Image<TColor> GetImage() => this.Factory.CreateImage(this.Width, this.Height);
        }

        private class FileProvider : TestImageProvider<TColor>
        {
            private static ConcurrentDictionary<string, Image<TColor>> cache =
                new ConcurrentDictionary<string, Image<TColor>>();

            private string filePath;

            public FileProvider(string filePath)
            {
                this.filePath = filePath;
            }

            public override string SourceFileOrDescription => this.filePath;

            public override Image<TColor> GetImage()
            {
                var cachedImage = cache.GetOrAdd(
                    this.filePath,
                    fn =>
                        {
                            var testFile = TestFile.Create(this.filePath);
                            return this.Factory.CreateImage(testFile.Bytes);
                        });

                return new Image<TColor>(cachedImage);
            }
        }

        private class LambdaProvider : TestImageProvider<TColor>
        {
            private readonly Func<GenericFactory<TColor>, Image<TColor>> creator;

            public LambdaProvider(Func<GenericFactory<TColor>, Image<TColor>> creator)
            {
                this.creator = creator;
            }

            public override Image<TColor> GetImage() => this.creator(this.Factory);
        }

        private class SolidProvider : BlankProvider
        {
            private readonly byte a;

            private readonly byte b;

            private readonly byte g;

            private readonly byte r;

            public SolidProvider(int width, int height, byte r, byte g, byte b, byte a)
                : base(width, height)
            {
                this.r = r;
                this.g = g;
                this.b = b;
                this.a = a;
            }

            public override string SourceFileOrDescription
                => $"Solid{this.Width}x{this.Height}_({this.r},{this.g},{this.b},{this.a})";

            public override Image<TColor> GetImage()
            {
                var image = base.GetImage();
                TColor color = default(TColor);
                color.PackFromBytes(this.r, this.g, this.b, this.a);

                return image.Fill(color);
            }
        }
    }

    /// <summary>
    /// Marker
    /// </summary>
    public interface ITestImageFactory
    {
    }
}