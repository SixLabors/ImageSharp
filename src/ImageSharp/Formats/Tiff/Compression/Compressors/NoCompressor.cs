// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff.Compression.Compressors
{
    internal class NoCompressor : TiffBaseCompressor
    {
        public NoCompressor(Stream output)
            : base(output, default, default, default)
        {
        }

        public override TiffEncoderCompression Method => TiffEncoderCompression.None;

        public override void Initialize(int rowsPerStrip)
        {
        }

        public override void CompressStrip(Span<byte> rows, int height) => this.Output.Write(rows);

        protected override void Dispose(bool disposing)
        {
        }
    }
}
