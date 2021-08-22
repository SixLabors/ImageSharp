// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
    internal enum CcittTwoDimensionalCodeType
    {
        None = 0,

        Pass = 1,

        Horizontal = 2,

        Vertical0 = 3,

        VerticalR1 = 4,

        VerticalR2 = 5,

        VerticalR3 = 6,

        VerticalL1 = 7,

        VerticalL2 = 8,

        VerticalL3 = 9,

        Extensions1D = 10,

        Extensions2D = 11,
    }
}
