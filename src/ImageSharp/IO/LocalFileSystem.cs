// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A wrapper around the local File apis.
    /// </summary>
    internal sealed class LocalFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public Stream OpenRead(string path) => File.OpenRead(path);

        /// <inheritdoc/>
        public Stream Create(string path) => File.Create(path);
    }
}
