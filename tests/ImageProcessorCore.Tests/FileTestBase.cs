// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.Collections.Generic;

    using Xunit;

    /// <summary>
    /// The test base class for reading and writing to files.
    /// </summary>
    public abstract class FileTestBase
    {
        /// <summary>
        /// The collection of image files to test against.
        /// </summary>
        protected static readonly List<string> Files = new List<string>
        {
            //TestImages.Png.P1,
            //TestImages.Png.Pd,
            //TestImages.Jpg.Floorplan, // Perf: Enable for local testing only
            TestImages.Jpg.Calliphora,
            //TestImages.Jpg.Cmyk, // Perf: Enable for local testing only
            //TestImages.Jpg.Turtle,
            //TestImages.Jpg.Fb, // Perf: Enable for local testing only
            //TestImages.Jpg.Progress, // Perf: Enable for local testing only
            //TestImages.Jpg.Gamma_dalai_lama_gray. // Perf: Enable for local testing only
            TestImages.Bmp.Car,
            //TestImages.Bmp.Neg_height, // Perf: Enable for local testing only
            //TestImages.Png.Blur, // Perf: Enable for local testing only
            //TestImages.Png.Indexed, // Perf: Enable for local testing only
            TestImages.Png.Splash,
            TestImages.Gif.Rings,
            //TestImages.Gif.Giphy // Perf: Enable for local testing only
        };

        protected void ProgressUpdate(object sender, ProgressEventArgs e)
        {
            Assert.InRange(e.RowsProcessed, 1, e.TotalRows);
        }
    }
}
