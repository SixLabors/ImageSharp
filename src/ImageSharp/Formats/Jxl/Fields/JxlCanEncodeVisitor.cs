// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal sealed class JxlCanEncodeVisitor : JxlVisitorBase
{
    private long encodedBits;
    private ulong extensions;
    private long posAfterExt;

    public bool OK { get; set; } = true;

    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        int enc = 0;
        this.OK &= JxlBitsCoder.CanEncode(bits, value, ref enc);
        this.encodedBits += enc;
        return true;
    }

    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
    {
        int encBits = 0;
        this.OK &= JxlU32Coder.CanEncode(enc, value, ref encBits);
        this.encodedBits += encBits;
        return true;
    }

    public override bool U64(ulong defaultValue, ref ulong value)
    {
        int encBits = 0;
        this.OK &= JxlU64Coder.CanEncode(value, ref encBits);
        this.encodedBits += encBits;
        return true;
    }

    public override bool F16(float defaultValue, ref float value)
    {
        int encBits = 0;
        this.OK &= JxlF16Coder.CanEncode(value, ref encBits);
        this.encodedBits += encBits;
        return true;
    }

    public override bool AllDefault(IJxlFields fields, ref bool allDefault)
    {
        allDefault = JxlBundle.AllDefault(fields);
        if (!this.Boolean(true, ref allDefault))
        {
            return false;
        }

        return allDefault;
    }

    public override bool BeginExtensions(ref ulong extensions)
    {
        if (!base.BeginExtensions(ref extensions))
        {
            return false;
        }

        this.extensions = extensions;

        if (extensions != 0uL)
        {
            if (this.posAfterExt != 0)
            {
                return false;
            }

            this.posAfterExt = this.encodedBits;

            if (this.posAfterExt == 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool GetSizes(ref int extensionBits, ref long totalBits)
    {
        if (!this.OK)
        {
            return false;
        }

        extensionBits = 0;
        totalBits = this.encodedBits;

        if (this.posAfterExt != 0)
        {
            if (this.encodedBits < this.posAfterExt)
            {
                return false;
            }

            extensionBits = (int)this.encodedBits - (int)this.posAfterExt;
            int encodedBits = 0;
            this.OK &= JxlU64Coder.CanEncode(extensionBits, ref encodedBits);
            totalBits += encodedBits;

            for (int i = 1; i < BitOperations.PopCount(this.extensions); i++)
            {
                encodedBits = 0;
                this.OK &= JxlU64Coder.CanEncode(0, ref encodedBits);
                totalBits += encodedBits;
            }
        }

        return true;
    }
}
