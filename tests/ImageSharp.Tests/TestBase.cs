// <copyright file="TestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    /// <summary>
    /// The test base class.
    /// </summary>
    public abstract class TestBase
    {
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