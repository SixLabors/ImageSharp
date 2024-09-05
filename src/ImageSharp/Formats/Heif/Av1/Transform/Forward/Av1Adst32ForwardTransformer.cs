// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Adst32ForwardTransformer : IAv1ForwardTransformer
{
    public void Transform(ref int input, ref int output, int cosBit, Span<byte> stageRange)
        => throw new NotImplementedException();

    public void TransformAvx2(ref Vector256<int> input, ref Vector256<int> output, int cosBit, int columnNumber)
        => throw new NotImplementedException();
}
