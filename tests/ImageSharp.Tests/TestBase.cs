// <copyright file="TestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using System.Reflection;

    using ImageSharp.Formats;

    /// <summary>
    /// The test base class. Inherit from this class for any image manipulation tests.
    /// </summary>
    public abstract class TestBase
    {
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
            string assemblyLocation = typeof(TestFile).GetTypeInfo().Assembly.Location;
            assemblyLocation = Path.GetDirectoryName(assemblyLocation);
            path = Path.GetFullPath(Path.Combine(assemblyLocation, "../../../TestOutput", path));

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