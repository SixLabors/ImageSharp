// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

// InlineArray values cannot be assigned as a property
#pragma warning disable SA1401 // Fields should be private

internal sealed class JpegHuffmanCodeTable
{
    public InlineArray256<byte> Depth;

    public InlineArray256<short> Code;

    public bool Initialized { get; set; }

    public void InitializeDepths(byte value = 0)
    {
        Span<byte> depths = this.Depth;
        depths.Fill(value);
    }
}
