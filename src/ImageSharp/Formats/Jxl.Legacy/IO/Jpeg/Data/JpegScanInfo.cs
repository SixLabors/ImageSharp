// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

// We may need to get a ref to fields, so don't
// make these properties.
#pragma warning disable SA1401 // Fields should be private

internal sealed class JpegScanInfo
{
    // Variables copied from ITU-T T.81 spec

    /// <summary>
    /// Start of spectral band in zigzag sequence
    /// </summary>
    public int Ss;

    /// <summary>
    /// End of spectral band in zigzag sequence
    /// </summary>
    public int Se;

    /// <summary>
    /// Successive approximation bit position. (High)
    /// </summary>
    public int Ah;

    /// <summary>
    /// Successive approximation bit position. (Low)
    /// </summary>
    public int Al;

    public int NumComponents;

    public InlineArray4<JpegComponentScanInfo> Components;

    public int LastNeededPass;

    public List<int> ResetPoints { get; set; } = [];

    public List<JpegExtraZeroRunInfo> ExtraZeroRuns { get; set; } = [];
}
