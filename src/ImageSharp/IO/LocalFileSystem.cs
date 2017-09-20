// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
/* Unmerged change from project 'ImageSharp(netstandard1.3)'
Before:
namespace SixLabors.ImageSharp.IO
{
 #if !NETSTANDARD1_1
After:
 #if !NETSTANDARD1_1
*/

 #if !NETSTANDARD1_1
    /// <summary>
    /// A wrapper around the local File apis.
    /// </summary>
    internal class LocalFileSystem : IFileSystem
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
