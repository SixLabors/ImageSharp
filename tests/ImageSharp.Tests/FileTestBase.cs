// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Collections.Generic;
    using ImageSharp.Formats;

    /// <summary>
    /// The test base class for reading and writing to files.
    /// </summary>
    public abstract class FileTestBase : TestBase
    {
        /// <summary>
        /// The collection of image files to test against.
        /// </summary>
        protected static readonly List<TestFile> Files = new List<TestFile>
        {
            TestFile.Create(TestImages.Jpeg.Baseline.Calliphora),
            TestFile.Create(TestImages.Jpeg.Baseline.Turtle),
            // TestFile.Create(TestImages.Jpeg.Baseline.Ycck), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Baseline.Cmyk), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Baseline.Floorplan), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Baseline.Bad.MissingEOF), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Progressive.Fb), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Progressive.Progress), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Baseline.GammaDalaiLamaGray), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Jpeg.Progressive.Bad.BadEOF), // Perf: Enable for local testing only
            TestFile.Create(TestImages.Bmp.Car),
            // TestFile.Create(TestImages.Bmp.Neg_height), // Perf: Enable for local testing only
            TestFile.Create(TestImages.Png.Splash),
            TestFile.Create(TestImages.Png.Powerpoint),
            // TestFile.Create(TestImages.Png.Blur), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Indexed), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.SplashInterlaced), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Interlaced), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Filter0), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Filter1), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Filter2), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Filter3), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Filter4), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.FilterVar), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.P1), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Png.Pd), // Perf: Enable for local testing only
            TestFile.Create(TestImages.Gif.Rings), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Gif.Cheers), // Perf: Enable for local testing only
            // TestFile.Create(TestImages.Gif.Giphy) // Perf: Enable for local testing only
        };
    }
}