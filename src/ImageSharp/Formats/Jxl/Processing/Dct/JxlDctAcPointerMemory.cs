// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

/// <summary>
/// Pointer to DCT AC coefficients using a <see cref="Memory{T}"/> backing
/// pointer type.
/// </summary>
internal ref struct JxlDctAcPointerMemory()
{
    /// <summary>
    /// 16-bit pointer (if any)
    /// </summary>
    public Memory<short> Pointer16 = Memory<short>.Empty;

    /// <summary>
    /// 32-bit pointer (if any)
    /// </summary>
    public Memory<int> Pointer32 = Memory<int>.Empty;
}
