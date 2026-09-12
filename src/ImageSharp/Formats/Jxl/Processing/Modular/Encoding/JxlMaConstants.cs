// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal static class JxlMaConstants
{
    /// <summary>
    /// Total number of MA tree contexts.
    /// </summary>
    public const int NumTreeContexts = 6;

    public const int MaxTreeSize = 1 << 22;

    public const int PropertyRangeFast = 512 << 4;

    public const int NumNonrefProperties = 2 + 13 + JxlContextPrediction.NumberOfProperties;

    public const int WpProp = NumNonrefProperties - JxlContextPrediction.NumberOfProperties;

    public const int GradientProp = 9;
}
