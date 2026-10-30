// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

#pragma warning disable SA1649 // File name should match first type name

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Used by JxlCustomTransformData
/// </summary>
[InlineArray(55)]
internal struct InlineArray55<T>
{
    private T first;
}

/// <summary>
/// Used by JpegQuantizationTable
/// </summary>
[InlineArray(64)]
internal struct InlineArray64<T>
{
    private T first;
}

/// <summary>
/// Used by JxlCustomTransformData
/// </summary>
[InlineArray(210)]
internal struct InlineArray210<T>
{
    private T first;
}
