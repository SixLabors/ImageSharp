// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1401 // Fields should be private

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

internal sealed class JxlOpsinParameters
{
    public InlineArray36<float> InverseOpsinMatrix;

    public InlineArray4<float> OpsinBiases;

    public InlineArray4<float> OpsinBiasesCbrt;

    public InlineArray4<float> QuantBiases;
}
