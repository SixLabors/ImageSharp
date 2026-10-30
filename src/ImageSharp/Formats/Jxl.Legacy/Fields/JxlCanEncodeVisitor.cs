// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Measures serialized fields and their extension payloads without writing bits.
/// </summary>
internal sealed class JxlCanEncodeVisitor : JxlVisitor
{
    private long encodedBits;
    private ulong extensions;
    private long posAfterExt;

    /// <summary>
    /// Gets whether every field in the current measurement can be encoded.
    /// </summary>
    public bool OK { get; private set; } = true;

    /// <inheritdoc />
    public override bool Bits(int bits, uint defaultValue, ref uint value)
    {
        int enc = 0;
        this.OK &= JxlBitsCoder.CanEncode(bits, value, ref enc);
        this.encodedBits += enc;
        return true;
    }

    /// <inheritdoc />
    public override bool U32(JxlU32Enc enc, uint defaultValue, ref uint value)
    {
        int encBits = 0;
        this.OK &= JxlU32Coder.CanEncode(enc, value, ref encBits);
        this.encodedBits += encBits;
        return true;
    }

    /// <inheritdoc />
    public override bool U64(ulong defaultValue, ref ulong value)
    {
        int encBits = 0;
        this.OK &= JxlU64Coder.CanEncode(value, ref encBits);
        this.encodedBits += encBits;
        return true;
    }

    /// <inheritdoc />
    public override bool F16(float defaultValue, ref float value)
    {
        int encBits = 0;
        this.OK &= JxlF16Coder.CanEncode(value, ref encBits);
        this.encodedBits += encBits;
        return true;
    }

    /// <inheritdoc />
    public override bool AllDefault(IJxlFields fields, ref bool allDefault)
    {
        allDefault = JxlBundle.AllDefault(fields);
        if (!this.Boolean(true, ref allDefault))
        {
            return false;
        }

        return allDefault;
    }

    /// <inheritdoc />
    public override bool VisitNested(IJxlFields fields)
    {
        int extensionBits = 0;
        long totalBits = 0;
        bool valid = this.Measure(fields, ref extensionBits, ref totalBits);
        this.OK &= valid;

        // A nested bundle's complete wire length includes its own extension-length fields.
        // Charge that complete length to the parent, including when the child occurs inside a parent extension.
        this.encodedBits += totalBits;
        return valid;
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Measures one bundle while preserving the active parent's counters and extension budget.
    /// </summary>
    /// <param name="fields">The fields to measure.</param>
    /// <param name="extensionBits">The current bundle's extension-payload length, excluding its length fields.</param>
    /// <param name="totalBits">The complete bundle length, including nested bundles and extension-length fields.</param>
    /// <returns>Whether all fields can be encoded and their extension payload fits the supported count.</returns>
    public bool Measure(IJxlFields fields, ref int extensionBits, ref long totalBits)
    {
        long parentBits = this.encodedBits;
        ulong parentExtensions = this.extensions;
        long parentExtensionPosition = this.posAfterExt;
        bool parentValid = this.OK;

        // Reuse this visitor for child measurements. Ordinary call locals retain the parent's state without allocating a scope object.
        // JxlVisitor.Visit owns the corresponding begun/ended flags and depth limit.
        this.encodedBits = 0;
        this.extensions = 0;
        this.posAfterExt = 0;
        this.OK = true;

        try
        {
            return this.Visit(fields) && this.GetSizes(ref extensionBits, ref totalBits);
        }
        finally
        {
            this.encodedBits = parentBits;
            this.extensions = parentExtensions;
            this.posAfterExt = parentExtensionPosition;
            this.OK = parentValid;
        }
    }

    /// <summary>
    /// Includes the current bundle's extension-length fields in its measured wire size.
    /// </summary>
    /// <param name="extensionBits">The payload length to assign to the first selected extension.</param>
    /// <param name="totalBits">The complete wire length for this bundle.</param>
    /// <returns>Whether the measured fields are valid and the payload length fits Int32.</returns>
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
            long payloadBits = this.encodedBits - this.posAfterExt;
            if (payloadBits < 0 || payloadBits > int.MaxValue)
            {
                return false;
            }

            // Match the existing encoder's layout: the first selected extension carries the complete payload.
            // Further selected extensions have zero-length payloads, but each still needs its own U64 length field.
            extensionBits = (int)payloadBits;
            int encodedBits = 0;
            this.OK &= JxlU64Coder.CanEncode((ulong)extensionBits, ref encodedBits);
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
