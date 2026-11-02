// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

/// <summary>
/// Huffman table indexes used for one component of one scan.
/// </summary>
// We may need to get a ref to fields, so don't
// make these properties.
internal struct JpegComponentScanInfo
{
    public int ComponentIndex;
    public int DcTableIndex;
    public int AcTableIndex;
}
