// <copyright file="TestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.Formats;

    /// <summary>
    /// The test base class. Inherit from this class for any image manipulation tests.
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// Initializes static members of the <see cref="TestBase"/> class.
        /// </summary>
        static TestBase()
        {
            // Register the individual image formats.
            Bootstrapper.Default.AddImageFormat(new PngFormat());
            Bootstrapper.Default.AddImageFormat(new JpegFormat());
            Bootstrapper.Default.AddImageFormat(new BmpFormat());
            Bootstrapper.Default.AddImageFormat(new GifFormat());
        }

        /// <summary>
        /// Creates the image output directory.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pathParts">The path parts.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        protected string CreateOutputDirectory(string path, params string[] pathParts)
        {
            path = Path.Combine("TestOutput", path);

            if (pathParts != null && pathParts.Length > 0)
            {
                path = Path.Combine(path, Path.Combine(pathParts));
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }
}