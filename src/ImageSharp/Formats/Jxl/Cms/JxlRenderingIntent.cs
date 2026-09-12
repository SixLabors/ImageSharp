// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

internal enum JxlRenderingIntent : byte
{
    // Values match ICC sRGB encodings
    Perceptual, // Good for photos, requires a profile with LUT
    Relative,   // Good for logos
    Saturation, // Perhaps useful for CG with fully saturated colors
    Absolute,   // Leaves white point unchanged; good for proofing
}
