// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

// We may need to get a ref to fields, so don't
// make these properties.
#pragma warning disable SA1401 // Fields should be private

/// <summary>
/// Represents one component of a jpeg file.
/// </summary>
internal sealed class JpegComponent
{
    /// <summary>
    /// One-byte id of the component
    /// </summary>
    public int Id;

    /// <summary>
    /// In interleaved mode, each minimal coded unit (MCU)
    /// has horizontal x vertical sample factor DCT blocks
    /// from this component. This is the horizontal factor.
    /// </summary>
    public int HorizontalSampleFactor = 1;

    /// <summary>
    /// In interleaved mode, each minimal coded unit (MCU)
    /// has horizontal x vertical sample factor DCT blocks
    /// from this component. This is the vertical factor.
    /// </summary>
    public int VerticalSampleFactor = 1;

    /// <summary>
    /// Index of quantization table used for this component.
    /// </summary>
    public int QuantIndex;

    /// <summary>
    /// Width measured in 8x8 blocks
    /// </summary>
    public int WidthInBlocks;

    /// <summary>
    /// Width measured in 8x8 blocks
    /// </summary>
    public int HeightInBlocks;

    /// <summary>
    /// Gets or sets DCT coefficients.
    /// </summary>
    public List<int> Coefficients { get; set; } = [];
}
