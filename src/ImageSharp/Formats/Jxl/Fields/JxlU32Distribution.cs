// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal struct JxlU32Distribution(uint d)
{
    public const uint DirectConstant = 0x80000000u;

    public readonly bool IsDirect => (d & DirectConstant) != 0;

    public readonly uint Direct => d & (DirectConstant - 1u);

    public readonly uint ExtraBits => (d & 0x1Fu) + 1u;

    public readonly uint Offset => (d >> 5) & 0x3FFFFFF;
}
