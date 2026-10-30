// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

internal struct JxlToken(JxlMaTreeContext c, uint value)
{
    public bool IsLz77Length;
    public JxlMaTreeContext Context = c;
    public uint Value = value;
}
