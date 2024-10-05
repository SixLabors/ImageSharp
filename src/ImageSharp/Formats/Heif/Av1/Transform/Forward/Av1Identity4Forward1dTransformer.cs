// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Identity4Forward1dTransformer : IAv1Forward1dTransformer
{
    public void Transform(ref int input, ref int output, int cosBit, Span<byte> stageRange)
        => throw new NotImplementedException();
}
