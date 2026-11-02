// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Writes measured JPEG XL fields through the existing primitive field coders.
/// </summary>
/// <param name="extensionBits">The current bundle's measured extension-payload length in bits.</param>
/// <param name="writer">The bit writer whose active budget covers the complete bundle.</param>
/// <param name="sizeVisitor">The reusable visitor used to measure nested extension payloads.</param>
internal sealed class JxlWriteVisitor(int extensionBits, JxlBitWriter writer, JxlCanEncodeVisitor sizeVisitor) : JxlVisitor
{
    /// <summary>
    /// The active bundle's extension payload, excluding its extension-length fields.
    /// </summary>
    private int extensionPayloadBits = extensionBits;

    /// <summary>
    /// Gets whether every primitive coder accepted its supplied field value.
    /// </summary>
    public bool OK { get; private set; } = true;

    /// <inheritdoc />
    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        this.OK &= JxlBitsCoder.Write(bits, value, writer);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
    {
        this.OK &= JxlU32Coder.Write(enc, value, writer);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool U64(ulong defaultValue, ref ulong value)
    {
        this.OK &= JxlU64Coder.Write(value, writer);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool F16(float defaultValue, ref float value)
    {
        this.OK &= JxlF16Coder.Write(value, writer);
        return this.OK;
    }

    /// <inheritdoc />
    public override bool VisitNested(IJxlFields fields)
    {
        int nestedExtensionBits = 0;
        long nestedTotalBits = 0;
        if (!sizeVisitor.Measure(fields, ref nestedExtensionBits, ref nestedTotalBits))
        {
            this.OK = false;
            return false;
        }

        // The parent's extension length already includes this child's complete wire size.
        // Keep writing under the same bit budget; only the extension-payload count changes for the nested scope.
        int parentExtensionBits = this.extensionPayloadBits;
        this.extensionPayloadBits = nestedExtensionBits;

        try
        {
            return base.VisitNested(fields);
        }
        finally
        {
            this.extensionPayloadBits = parentExtensionBits;
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

        // The encoder assigns all payload bits to the lowest selected extension.
        // Other selected extensions still serialize a zero-length field. These are bit counts, with no byte-alignment padding.
        this.OK &= JxlU64Coder.Write((ulong)this.extensionPayloadBits, writer);
        for (ulong remaining = extensions & (extensions - 1); remaining != 0; remaining &= remaining - 1)
        {
            this.OK &= JxlU64Coder.Write(0, writer);
        }

        return this.OK;
    }
}
