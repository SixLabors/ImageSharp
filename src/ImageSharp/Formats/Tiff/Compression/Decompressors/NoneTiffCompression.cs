// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Decompressors
{
    /// <summary>
    /// Class to handle cases where TIFF image data is not compressed.
    /// </summary>
    internal class NoneTiffCompression : TiffBaseDecompresor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoneTiffCompression" /> class.
        /// </summary>
        public NoneTiffCompression()
            : base(default, default, default)
        {
        }

        /// <inheritdoc/>
        protected override void Decompress(BufferedReadStream stream, int byteCount, Span<byte> buffer) => _ = stream.Read(buffer, 0, Math.Min(buffer.Length, byteCount));

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
        }
    }
}
