// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

/// <summary>
/// Represents the JPEG XL Bit Depth image metadata.
/// </summary>
internal sealed class JxlBitDepth : IJxlFields
{
    private uint bitsPerSample;
    private uint exponentBitsPerSample;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBitDepth"/> class.
    /// </summary>
    public JxlBitDepth() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets a value indicating whether
    /// the original (uncompressed) samples are floating point or
    /// unsigned integer.
    /// </summary>
    public bool FloatingPointSample { get; set; }

    /// <summary>
    /// Gets or sets the bit depth of the original (uncompressed) image samples.
    /// Must be in the range [1, 32].
    /// </summary>
    public uint BitsPerSample
    {
        get => this.bitsPerSample;
        set => this.bitsPerSample = value;
    }

    /// <summary>
    /// <para>
    ///   Gets or sets floating point exponent bits of the original (uncompressed) image samples,
    ///   only used if <see cref="FloatingPointSample"/>  is <see langword="true"/>.
    /// </para>
    /// <para>
    ///   If used, the samples are floating point with:
    ///   <list type="bullet">
    ///     <item>1 sign bit</item>
    ///     <item><see cref="ExponentBitsPerSample"/> exponent bits</item>
    ///     <item>(<see cref="BitsPerSample"/> - <see cref="ExponentBitsPerSample"/> - 1) mantissa bits</item>
    ///   </list>
    ///   If used, <see cref="ExponentBitsPerSample"/> must be in the range
    ///   [2, 8] and amount of mantissa bits must be in the range [2, 23].
    /// </para>
    /// </summary>
    public uint ExponentBitsPerSample
    {
        get => this.exponentBitsPerSample;
        set => this.exponentBitsPerSample = value;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (!this.FloatingPointSample)
        {
            bool successful = visitor.U32(
                JxlFieldExpressions.Value(8u),
                JxlFieldExpressions.Value(10u),
                JxlFieldExpressions.Value(12u),
                JxlFieldExpressions.BitsOffset(6u, 1u),
                8u,
                ref this.bitsPerSample);

            if (!successful)
            {
                return false;
            }

            this.exponentBitsPerSample = 0;
        }
        else
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(32u),
                JxlFieldExpressions.Value(16u),
                JxlFieldExpressions.Value(24u),
                JxlFieldExpressions.BitsOffset(6u, 1u),
                32u,
                ref this.bitsPerSample))
            {
                return false;
            }

            this.exponentBitsPerSample--;

            if (!visitor.Bits(4, 7, ref this.exponentBitsPerSample))
            {
                return false;
            }

            this.exponentBitsPerSample++;
        }

        if (this.FloatingPointSample)
        {
            if (this.exponentBitsPerSample is < 2 or > 8)
            {
                DebugGuard.IsTrue(false, "Invalid exponent_bits_per_sample");

                return false;
            }

            int mantissaBits = (int)this.bitsPerSample - (int)this.exponentBitsPerSample - 1;

            if (mantissaBits is < 2 or > 23)
            {
                DebugGuard.IsTrue(false, "Invalid bits_per_sample");

                return false;
            }
        }
        else if (this.bitsPerSample > 31)
        {
            DebugGuard.IsTrue(false, "Invalid bits_per_sample");

            return false;
        }

        return true;
    }
}
