// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal sealed class JpegSerializationState
{
    public JpegSerializationStage Stage { get; set; } = JpegSerializationStage.Initialize;

    public int SectionIndex { get; set; }

    public int DhtIndex { get; set; }

    public int DqtIndex { get; set; }

    public int AppIndex { get; set; }

    public int ComIndex { get; set; }

    public int DataIndex { get; set; }

    public int ScanIndex { get; set; }

    public JpegHuffmanCodeTable[] DcHuffTable { get; set; } = [];

    public JpegHuffmanCodeTable[] AcHuffTable { get; set; } = [];

    // Should be field to get ref to it
    public ReadOnlyMemory<byte> PadBits;

    public int PadBitsPos { get; set; }

    public bool SeenDriMarker { get; set; }

    public bool IsProgressive { get; set; }

    public List<List<byte>> OutputQueue { get; set; } = [];

    public EncodeScanState ScanState { get; set; }
}
