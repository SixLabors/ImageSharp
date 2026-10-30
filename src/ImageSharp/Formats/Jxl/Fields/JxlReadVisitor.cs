// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Reads serialized JPEG XL fields and validates extension bit budgets.
/// </summary>
/// <param name="reader">The bit reader for the current bounded header.</param>
internal sealed class JxlReadVisitor(JxlBitReader reader) : JxlVisitor
{
    /// <summary>
    /// The total declared extension payload length in bits.
    /// </summary>
    private ulong totalExtensionBits;

    /// <summary>
    /// The bit position after the extension lengths, or zero when no extensions are present.
    /// </summary>
    private long posAfterExtSize;

    /// <summary>
    /// Gets whether decoded field values satisfy their format constraints.
    /// </summary>
    public bool OK { get; private set; } = true;

    /// <inheritdoc />
    public override bool IsReading => true;

    /// <inheritdoc />
    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        value = JxlBitsCoder.Read(bits, reader);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
    {
        value = JxlU32Coder.Read(enc, reader);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool U64(ulong defaultValue, ref ulong value)
    {
        value = JxlU64Coder.Read(reader);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool F16(float defaultValue, ref float value)
    {
        this.OK &= JxlF16Coder.Read(reader, ref value);
        return this.OK;
    }

    /// <inheritdoc />
    public override void SetDefault(IJxlFields fields) => JxlBundle.SetDefault(fields);

    /// <inheritdoc />
    public override bool VisitNested(IJxlFields fields)
    {
        ulong parentExtensionBits = this.totalExtensionBits;
        long parentExtensionPosition = this.posAfterExtSize;

        // Each nested bundle carries its own extension lengths. A child with no extensions must not skip the parent's remaining payload.
        // Preserve both counters in call locals while JxlVisitor maintains the nested begun/ended flags.
        this.totalExtensionBits = 0;
        this.posAfterExtSize = 0;

        try
        {
            return base.VisitNested(fields);
        }
        finally
        {
            this.totalExtensionBits = parentExtensionBits;
            this.posAfterExtSize = parentExtensionPosition;
        }
    }

    /// <inheritdoc />
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
            ulong extensionBits = 0;

            if (!this.U64(0, ref extensionBits))
            {
                return false;
            }

            // Each set flag carries one length. Check remaining UInt64 capacity before adding
            // the untrusted length; wrapped totals would skip less data than the header declares.
            if (extensionBits > ulong.MaxValue - this.totalExtensionBits)
            {
                return false;
            }

            this.totalExtensionBits += extensionBits;
        }

        this.posAfterExtSize = reader.TotalBitsConsumed;
        return this.posAfterExtSize != 0;
    }

    /// <inheritdoc />
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

        long bitsRead = reader.TotalBitsConsumed;

        // Bit positions use Int64. Reject lengths that cannot form a representable end position
        // before converting or adding them. Invalid header lengths are not debug-only failures.
        if (this.totalExtensionBits > (ulong)(long.MaxValue - this.posAfterExtSize))
        {
            return false;
        }

        long end = this.posAfterExtSize + (long)this.totalExtensionBits;
        if (bitsRead > end)
        {
            return false;
        }

        long remainingBits = end - bitsRead;

        if (remainingBits != 0)
        {
            reader.SkipBits64((ulong)remainingBits);
        }

        return true;
    }
}
