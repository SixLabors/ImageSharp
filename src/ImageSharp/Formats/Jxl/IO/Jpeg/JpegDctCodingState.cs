// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal sealed class JpegDctCodingState
{
    public int EobRun { get; set; }

    public JpegHuffmanCodeTable? CurAcHuff { get; set; }

    public List<ushort> RefinementBits { get; } = new(64);

    public int RefinementBitsCount { get; set; }

    public void Flush(JpegBitWriter writer) => JpegWriter.Flush(this, writer);
}
