// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

internal enum Av1ComponentType
{
    Luminance = 0, // luma
    Chroma = 1, // chroma (Cb+Cr)
    ChromaCb = 2, // chroma Cb
    ChromaCr = 3, // chroma Cr
    All = 4, // Y+Cb+Cr
    None = 15
}
