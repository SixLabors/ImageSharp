// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Optimize;

internal delegate T JxlOptimizeFunction<T>(JxlOptimizeArray<T> a, ref JxlOptimizeArray<T> b)
    where T : unmanaged, INumber<T>;
