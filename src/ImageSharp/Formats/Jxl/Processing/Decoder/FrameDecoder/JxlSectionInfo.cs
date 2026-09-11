// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.FrameDecoder;

internal sealed class JxlSectionInfo(JxlBitReader reader, int id, int index)
{
    public JxlBitReader BitReader { get; set; } = reader;

    public int Id { get; set; } = id;

    public int Index { get; set; } = index;
}
