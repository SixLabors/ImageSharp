// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlQuantWeights
{
    public const int MaxQuantTableSize = JxlAcStrategy.MaximumCoefficientArea;

    public const int NumPredefinedTables = 1;

    public const int CeilLog2NumPredefinedTables = 0;

    public const int Log2NumQuantModes = 3;
}
