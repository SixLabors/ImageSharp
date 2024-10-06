// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1BitDepthExtensions
{
    public static int GetBitCount(this Av1BitDepth bitDepth) => 8 + ((int)bitDepth << 1);
}
