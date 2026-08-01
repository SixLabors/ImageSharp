// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

#pragma warning disable SA1649 // File name should match first type name

namespace SixLabors.ImageSharp.Formats.Jxl;

[InlineArray(3)]
internal struct InlineArray3<T>
{
    private T first;
}

/// <summary>
/// Used by JxlOpsinParameters
/// </summary>
[InlineArray(36)]
internal struct InlineArray36<T>
{
    private T first;
}

/// <summary>
/// Used by JxlCustomTransformData
/// </summary>
[InlineArray(15)]
internal struct InlineArray15<T>
{
    private T first;
}

/// <summary>
/// Used by JxlCustomTransformData
/// </summary>
[InlineArray(55)]
internal struct InlineArray55<T>
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

/// <summary>
/// Used by JxlWeightsSeparable5
/// </summary>
[InlineArray(12)]
internal struct InlineArray12<T>
{
    private T first;
}
