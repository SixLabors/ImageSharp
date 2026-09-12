// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

internal sealed class JxlAnsHybridUIntConfiguration : IJxlFields
{
    public JxlAnsHybridUIntConfiguration(uint splitExponent = 4, uint msbInToken = 2, uint lsbInToken = 0)
    {
        this.SplitExponent = splitExponent;
        this.SplitToken = 1u << (int)splitExponent;
        this.MsbInToken = msbInToken;
        this.LsbInToken = lsbInToken;

        if (splitExponent < msbInToken + lsbInToken)
        {
            throw new InvalidOperationException("Split exponent should be < msbInToken + lsbInToken");
        }
    }

    public uint SplitExponent { get; set; }

    public uint SplitToken { get; set; }

    public uint MsbInToken { get; set; } // Most significant bit

    public uint LsbInToken { get; set; } // Least significant bit

    public uint LsbMask => (1u << (int)this.LsbInToken) - 1;

    public void Encode(uint value, out uint token, out uint bitCount, out uint bits)
    {
        if (value < this.SplitToken)
        {
            token = value;
            bitCount = 0;
            bits = 0;
        }
        else
        {
            uint n = JxlMath.FloorLog2Nonzero(value);
            uint m = value - (1u << (int)n);

            unchecked
            {
                // The following expression is quite complex.
                // See https://github.com/libjxl/libjxl/blob/main/lib/jxl/dec_ans.h#L83C16-L86C47.
                token = this.SplitToken +
                (uint)(((n - this.SplitExponent) << (int)(this.MsbInToken + this.LsbInToken)) +
                        ((m >> (int)(n - this.MsbInToken)) << (int)this.LsbInToken) +
                        (m & ((1 << (int)this.LsbInToken) - 1)));

                bitCount = n - this.MsbInToken - this.LsbInToken;
                bits = (value >> (int)this.LsbInToken) & ((1u << (int)bitCount) - 1);
            }
        }
    }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}
