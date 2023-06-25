// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

public enum QoiChunkEnum
{
    QOI_OP_RGB = 0b11111110,
    QOI_OP_RGBA = 0b11111111,
    QOI_OP_INDEX = 0b00000000,
    QOI_OP_DIFF = 0b01000000,
    QOI_OP_LUMA = 0b10000000,
    QOI_OP_RUN = 0b11000000
}
