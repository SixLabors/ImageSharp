// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1EchoTestTransformer : IAv1Forward1dTransformer
{
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
        => input.CopyTo(output);
}
