// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal struct JxlResidualToken
{
    public int Token;
    public int NumberOfBits;

    public JxlResidualToken(int token, int numberOfBits)
    {
        this.Token = token;
        this.NumberOfBits = numberOfBits;
    }
}
