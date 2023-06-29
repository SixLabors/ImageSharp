// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Enum that contains the operations that encoder and decoder must process, written
/// in binary to be easier to compare them in the reference
/// </summary>
internal enum QoiChunk
{
    /// <summary>
    /// Indicates that the operation is QOI_OP_RGB where the RGB values are written
    /// in one byte each one after this marker
    /// </summary>
    QoiOpRgb = 0b11111110,

    /// <summary>
    /// Indicates that the operation is QOI_OP_RGBA where the RGBA values are written
    /// in one byte each one after this marker
    /// </summary>
    QoiOpRgba = 0b11111111,

    /// <summary>
    /// Indicates that the operation is QOI_OP_INDEX where one byte contains a 2-bit
    /// marker (0b00) followed by an index on the previously seen pixels array 0..63
    /// </summary>
    QoiOpIndex = 0b00000000,

    /// <summary>
    /// Indicates that the operation is QOI_OP_DIFF where one byte contains a 2-bit
    /// marker (0b01) followed by 2-bit differences in red, green and blue channel
    /// with the previous pixel with a bias of 2 (-2..1)
    /// </summary>
    QoiOpDiff = 0b01000000,

    /// <summary>
    /// Indicates that the operation is QOI_OP_LUMA where one byte contains a 2-bit
    /// marker (0b01) followed by a 6-bits number that indicates the difference of
    /// the green channel with the previous pixel. Then another byte that contains
    /// a 4-bit number that indicates the difference of the red channel minus the
    /// previous difference, and another 4-bit number that indicates the difference
    /// of the blue channel minus the green difference
    /// Example: 0b10[6-bits diff green] 0b[6-bits dr-dg][6-bits db-dg]
    /// dr_dg = (cur_px.r - prev_px.r) - (cur_px.g - prev_px.g)
    /// db_dg = (cur_px.b - prev_px.b) - (cur_px.g - prev_px.g)
    /// </summary>
    QoiOpLuma = 0b10000000,

    /// <summary>
    /// Indicates that the operation is QOI_OP_RUN where one byte contains a 2-bit
    /// marker (0b11) followed by a 6-bits number that indicates the times that the
    /// previous pixel is repeated
    /// </summary>
    QoiOpRun = 0b11000000
}
