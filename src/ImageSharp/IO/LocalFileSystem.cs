// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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