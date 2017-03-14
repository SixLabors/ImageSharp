using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImageSharp.IO
{
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
        public Stream OpenWrite(string path)
        {
            return File.OpenWrite(path);
        }
    }
#endif
}
