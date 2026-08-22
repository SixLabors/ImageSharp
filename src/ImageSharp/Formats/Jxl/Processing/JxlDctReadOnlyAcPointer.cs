// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Pointer to DCT AC coefficients. (Read-only)
/// </summary>
internal readonly ref struct JxlDctReadOnlyAcPointer
{
    /// <summary>
    /// 16-bit pointer (if any)
    /// </summary>
    public readonly ReadOnlySpan<short> Pointer16;

    /// <summary>
    /// 32-bit pointer (if any)
    /// </summary>
    public readonly ReadOnlySpan<int> Pointer32;

    internal JxlDctReadOnlyAcPointer(ReadOnlySpan<short> pointer16) => this.Pointer16 = pointer16;

    internal JxlDctReadOnlyAcPointer(ReadOnlySpan<int> pointer32) => this.Pointer32 = pointer32;
}
