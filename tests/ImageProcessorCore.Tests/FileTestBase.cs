// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;
    using System.Collections.Generic;

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
            //new TestFile(TestImages.Png.P1),
            //new TestFile(TestImages.Png.Pd),
            new TestFile(TestImages.Jpg.Floorplan), // Perf: Enable for local testing only
            new TestFile(TestImages.Jpg.Calliphora),
            //new TestFile(TestImages.Jpg.Cmyk), // Perf: Enable for local testing only
            //new TestFile(TestImages.Jpg.Turtle),
            //new TestFile(TestImages.Jpg.Fb), // Perf: Enable for local testing only
            //new TestFile(TestImages.Jpg.Progress), // Perf: Enable for local testing only
            //new TestFile(TestImages.Jpg.Gamma_dalai_lama_gray). // Perf: Enable for local testing only
            new TestFile(TestImages.Bmp.Car),
            //new TestFile(TestImages.Bmp.Neg_height), // Perf: Enable for local testing only
            //new TestFile(TestImages.Png.Blur), // Perf: Enable for local testing only
            //new TestFile(TestImages.Png.Indexed), // Perf: Enable for local testing only
            new TestFile(TestImages.Png.Splash),
            new TestFile(TestImages.Gif.Rings),
            //new TestFile(TestImages.Gif.Giphy) // Perf: Enable for local testing only
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