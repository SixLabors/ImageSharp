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
        /// <summary>
        /// Returns an <see cref="Image{TColor}"/> instance to the test case with the necessary traits.
        /// </summary>
        public abstract Image<TColor> GetImage();

        public virtual string SourceFileOrDescription => "";

        /// <summary>
        /// Utility instance to provide informations about the test image & manage input/output
        /// </summary>
        public ImagingTestCaseUtility Utility { get; private set; }

        protected virtual TestImageProvider<TColor> InitUtility(MethodInfo testMethod)
        {
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
            protected int Width { get; }

            protected int Height { get; }

            public BlankProvider(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }

            public override string SourceFileOrDescription => $"Blank{this.Width}x{this.Height}";

            public override Image<TColor> GetImage() => new Image<TColor>(this.Width, this.Height);
        }

        public static TestImageProvider<TColor> Blank(int width, int height, MethodInfo testMethod = null)
            => new BlankProvider(width, height).InitUtility(testMethod);

        private class LambdaProvider : TestImageProvider<TColor>
        {
            private readonly Func<Image<TColor>> creator;

            public LambdaProvider(Func<Image<TColor>> creator)
            {
                this.creator = creator;
            }

            public override Image<TColor> GetImage() => this.creator();
        }

        public static TestImageProvider<TColor> Lambda(
            Func<Image<TColor>> func,
            MethodInfo testMethod = null) => new LambdaProvider(func).InitUtility(testMethod);

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
                            var testFile = TestFile.CreateWithoutImage(this.filePath);
                            return new Image<TColor>(testFile.Bytes);
                        });

                return new Image<TColor>(cachedImage);
            }
        }

        public static TestImageProvider<TColor> File(string filePath, MethodInfo testMethod = null)
        {
            return new FileProvider(filePath).InitUtility(testMethod);
        }

        private class SolidProvider : BlankProvider
        {
            private readonly byte r;

            private readonly byte g;

            private readonly byte b;

            private readonly byte a;

            public override Image<TColor> GetImage()
            {
                var image = base.GetImage();
                TColor color = default(TColor);
                color.PackFromBytes(this.r, this.g, this.b, this.a);

                return image.Fill(color);
            }

            public SolidProvider(int width, int height, byte r, byte g, byte b, byte a)
                : base(width, height)
            {
                this.r = r;
                this.g = g;
                this.b = b;
                this.a = a;
            }
        }

        public static TestImageProvider<TColor> Solid(
            int width,
            int height,
            byte r,
            byte g,
            byte b,
            byte a = 255,
            MethodInfo testMethod = null)
        {
            return new SolidProvider(width, height, r, g, b, a).InitUtility(testMethod);
        }
    }

    /// <summary>
    /// Marker
    /// </summary>
    public interface ITestImageFactory
    {
    }
}
