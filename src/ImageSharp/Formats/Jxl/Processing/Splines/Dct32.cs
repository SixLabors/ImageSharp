// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

/// <summary>
/// Storage for 32 DCT coefficients (floating-point).
/// </summary>
[InlineArray(32)]
internal struct Dct32
{
    private float first;
}
