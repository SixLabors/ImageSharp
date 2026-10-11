// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Optimize;

internal class JxlOptimizeArrayFactory<T>(Configuration configuration, int length)
    where T : unmanaged, INumber<T>
{
    public JxlOptimizeArray<T> CreateArray(T defaultValue = default) => new(configuration, length, defaultValue);
}
