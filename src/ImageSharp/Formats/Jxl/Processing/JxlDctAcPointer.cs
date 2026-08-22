// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Pointer to DCT AC coefficients
/// </summary>
internal ref struct JxlDctAcPointer()
{
    /// <summary>
    /// 16-bit pointer (if any)
    /// </summary>
    public Span<short> Pointer16 = [];

    /// <summary>
    /// 32-bit pointer (if any)
    /// </summary>
    public Span<int> Pointer32 = [];
}
