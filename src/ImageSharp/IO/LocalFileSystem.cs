// <copyright file="LocalFileSystem.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

 #if !NETSTANDARD1_1
    /// <summary>
    /// A wrapper around the local File apis.
    /// </summary>
    public class LocalFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public Stream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        /// <inheritdoc/>
        public Stream Create(string path)
        {
            return File.Create(path);
        }
    }
#endif
}
