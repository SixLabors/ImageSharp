// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

internal sealed class JxlReadVisitor(JxlBitReader reader) : JxlVisitorBase
{
    private ulong totalExtensionBits;
    private bool notEnoughBytes;
    private long posAfterExtSize;
    private readonly ulong[] extensionBits = new ulong[JxlBundle.MaxExtensions];

    public bool OK { get; private set; }

    public override bool IsReading => true;

    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        value = JxlBitsCoder.Read(bits, reader);
        return this.ThrowIfEndOfStreamOrReturnTrue();
    }

    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
    {
        value = JxlU32Coder.Read(enc, reader);
        return this.ThrowIfEndOfStreamOrReturnTrue();
    }

    public override bool U64(ulong defaultValue, ref ulong value)
    {
        value = JxlU64Coder.Read(reader);
        return this.ThrowIfEndOfStreamOrReturnTrue();
    }

    public override bool F16(float defaultValue, ref float value)
    {
        this.OK &= JxlF16Coder.Read(reader, ref value);
        return this.ThrowIfEndOfStreamOrReturnTrue();
    }

    public override void SetDefault(IJxlFields fields) => JxlBundle.SetDefault(fields);

    public override bool BeginExtensions(ref ulong extensions)
    {
        if (!base.BeginExtensions(ref extensions))
        {
            return false;
        }

        if (extensions == 0)
        {
            return true;
        }

        for (ulong remainingExtensions = extensions; remainingExtensions != 0; remainingExtensions &= remainingExtensions - 1)
        {
            ulong idxExtension = JxlMath.Num0BitsBelowLS1Bit_Nonzero(remainingExtensions);
            if (!this.U64(0, ref this.extensionBits[idxExtension]))
            {
                return false;
            }

            if (!JxlMath.SafeAdd(this.totalExtensionBits, this.extensionBits[idxExtension], ref this.totalExtensionBits))
            {
                DebugGuard.IsTrue(false, "Extension bits overflow; the codestream is not valid");

                return false;
            }
        }

        this.posAfterExtSize = reader.TotalBitsConsumed;
        return this.posAfterExtSize != 0;
    }

    public override bool EndExtensions()
    {
        if (!base.EndExtensions())
        {
            return false;
        }

        if (this.posAfterExtSize == 0)
        {
            return true;
        }

        if (this.notEnoughBytes)
        {
            return true;
        }

        long bitsRead = reader.TotalBitsConsumed;

        long end = 0;
        if (!JxlMath.SafeAdd(this.posAfterExtSize, this.totalExtensionBits, ref end))
        {
            DebugGuard.IsTrue(false, "Invalid extension size.");

            return false;
        }

        if (bitsRead > end)
        {
            DebugGuard.IsTrue(false, "Read more extension bits than budgeted");

            return false;
        }

        long remainingBits = end - bitsRead;

        if (remainingBits != 0)
        {
            reader.SkipBits64((uint)remainingBits);
        }

        return true;
    }
}
