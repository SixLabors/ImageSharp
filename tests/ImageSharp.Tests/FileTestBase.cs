// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// The test base class for reading and writing to files.
    /// </summary>
    public abstract class FileTestBase
    {
        /// <summary>
        /// The collection of image files to test against.
        /// </summary>
        protected static readonly List<TestFile> Files = new List<TestFile>
        {
            // new TestFile(TestImages.Png.P1), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Pd), // Perf: Enable for local testing only
            // new TestFile(TestImages.Jpeg.Floorplan), // Perf: Enable for local testing only
            new TestFile(TestImages.Jpeg.Calliphora),
            // new TestFile(TestImages.Jpeg.Cmyk), // Perf: Enable for local testing only
            new TestFile(TestImages.Jpeg.Turtle),
            // new TestFile(TestImages.Jpeg.Fb), // Perf: Enable for local testing only
            // new TestFile(TestImages.Jpeg.Progress), // Perf: Enable for local testing only
            // new TestFile(TestImages.Jpeg.GammaDalaiLamaGray), // Perf: Enable for local testing only
            new TestFile(TestImages.Bmp.Car),
            // new TestFile(TestImages.Bmp.Neg_height), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Blur), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Indexed), // Perf: Enable for local testing only
            new TestFile(TestImages.Png.Splash),
            new TestFile(TestImages.Png.SplashInterlaced),
            new TestFile(TestImages.Png.Interlaced),
            // new TestFile(TestImages.Png.Filter0), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Filter1), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Filter2), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Filter3), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.Filter4), // Perf: Enable for local testing only
            // new TestFile(TestImages.Png.FilterVar), // Perf: Enable for local testing only
            new TestFile(TestImages.Gif.Rings),
            // new TestFile(TestImages.Gif.Giphy) // Perf: Enable for local testing only
        };

        protected string CreateOutputDirectory(string path)
        {
            path = "TestOutput/" + path;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }
}