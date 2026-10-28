// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

// Public fields because InlineArray2 values
// cannot be mutated if it's a property
#pragma warning disable SA1401 // Fields should be private

internal sealed class ButteraugliPsychoImage
{
    public InlineArray2<JxlPlane<float>> Uhf; // XY

    public InlineArray2<JxlPlane<float>> Hf; // XY

    public JxlImage3<float>? Mf { get; set; } // XYB

    public JxlImage3<float>? Lf { get; set; } // XYB
}
