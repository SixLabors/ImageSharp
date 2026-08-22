// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal enum JxlExifOrientation : byte
{
    Identity = 1,
    FlipHorizontal = 2,
    Rotate180 = 3,
    FlipVertical = 4,
    Transponse = 5,
    Rotate90 = 6,
    AntiTranspose = 7,
    Rotate270 = 8
}
