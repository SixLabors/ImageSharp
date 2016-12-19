// <copyright file="TestImageFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.TestUtilities
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Provides <see cref="Image{TColor,TPacked}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TColor">The pixel format of the image</typeparam>
    /// <typeparam name="TPacked">The packed format of the image</typeparam>
    public abstract class TestImageFactory<TColor, TPacked> : ITestImageFactory
        where TPacked : struct, IEquatable<TPacked> where TColor : struct, IPackedPixel<TPacked>
    {
        public abstract Image<TColor, TPacked> Create();

        public virtual string SourceFileOrDescription => "";

        /// <summary>
        /// Utility instance to provide informations about the test image & manage input/output
        /// </summary>
        public ImagingTestCaseUtility Utility { get; private set; }

        protected TestImageFactory()
        {
        }

        protected virtual TestImageFactory<TColor, TPacked> InitUtility(MethodInfo testMethod)
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

        private class BlankFactory : TestImageFactory<TColor, TPacked>
        {
            protected int Width { get; }

            protected int Height { get; }

            public BlankFactory(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }

            public override string SourceFileOrDescription => $"Blank{this.Width}x{this.Height}";

            public override Image<TColor, TPacked> Create() => new Image<TColor, TPacked>(this.Width, this.Height);
        }

        public static TestImageFactory<TColor, TPacked> Blank(int width, int height, MethodInfo testMethod = null)
            => new BlankFactory(width, height).InitUtility(testMethod);

        private class LambdaFactory : TestImageFactory<TColor, TPacked>
        {
            private readonly Func<Image<TColor, TPacked>> creator;

            public LambdaFactory(Func<Image<TColor, TPacked>> creator)
            {
                this.creator = creator;
            }

            public override Image<TColor, TPacked> Create() => this.creator();
        }

        public static TestImageFactory<TColor, TPacked> Lambda(
            Func<Image<TColor, TPacked>> func,
            MethodInfo testMethod = null) => new LambdaFactory(func).InitUtility(testMethod);

        private class FileFactory : TestImageFactory<TColor, TPacked>
        {
            private string filePath;

            public FileFactory(string filePath)
            {
                this.filePath = filePath;
            }

            public override string SourceFileOrDescription => this.filePath;

            public override Image<TColor, TPacked> Create()
            {
                using (var stream = System.IO.File.OpenRead(this.filePath))
                {
                    return new Image<TColor, TPacked>(stream);
                }
            }
        }

        public static TestImageFactory<TColor, TPacked> File(string filePath, MethodInfo testMethod = null)
        {
            return new FileFactory(filePath).InitUtility(testMethod);
        }

        private class SolidFactory : BlankFactory
        {
            private readonly byte r;

            private readonly byte g;

            private readonly byte b;

            private readonly byte a;

            public override Image<TColor, TPacked> Create()
            {
                var image = base.Create();
                TColor color = default(TColor);
                color.PackFromBytes(this.r, this.g, this.b, this.a);

                return image.Fill(color);
            }

            public SolidFactory(int width, int height, byte r, byte g, byte b, byte a)
                : base(width, height)
            {
                this.r = r;
                this.g = g;
                this.b = b;
                this.a = a;
            }
        }

        public static TestImageFactory<TColor, TPacked> Solid(
            int width,
            int height,
            byte r,
            byte g,
            byte b,
            byte a = 255,
            MethodInfo testMethod = null)
        {
            return new SolidFactory(width, height, r, g, b, a).InitUtility(testMethod);
        }
    }

    /// <summary>
    /// Marker
    /// </summary>
    public interface ITestImageFactory
    {
    }
}
