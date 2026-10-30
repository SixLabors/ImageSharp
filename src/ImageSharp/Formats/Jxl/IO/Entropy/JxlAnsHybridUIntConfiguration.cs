// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

/// <summary>
/// Specifies how an unsigned integer is divided between an entropy token and its remaining bit payload.
/// </summary>
/// <remarks>
/// The entropy reader prepares every configuration before processing tokens. Configuration values remain fixed for that entropy stream.
/// This value stores the four native UInt32 fields together, without a separate object for each histogram or token calculation.
/// </remarks>
internal readonly struct JxlAnsHybridUIntConfiguration
{
    /// <summary>
    /// Initializes the default split exponent of four, with two high token bits and no low token bits.
    /// </summary>
    public JxlAnsHybridUIntConfiguration()
        : this(4, 2, 0)
    {
    }

    /// <summary>
    /// Initializes the token split and its derived direct-token threshold.
    /// </summary>
    /// <param name="splitExponent">The base-two exponent of the direct-token threshold.</param>
    /// <param name="msbInToken">The number of high value bits stored in the token.</param>
    /// <param name="lsbInToken">The number of low value bits stored in the token.</param>
    public JxlAnsHybridUIntConfiguration(uint splitExponent, uint msbInToken, uint lsbInToken)
    {
        if (splitExponent < msbInToken + lsbInToken)
        {
            throw new InvalidOperationException("Split exponent must be at least the sum of the high and low token bit counts.");
        }

        this.SplitExponent = splitExponent;
        this.SplitToken = 1U << (int)splitExponent;
        this.MsbInToken = msbInToken;
        this.LsbInToken = lsbInToken;
    }

    /// <summary>
    /// Gets the base-two exponent of the direct-token threshold.
    /// </summary>
    public uint SplitExponent { get; }

    /// <summary>
    /// Gets the direct-token threshold. Smaller input values are represented directly by their token.
    /// </summary>
    public uint SplitToken { get; }

    /// <summary>
    /// Gets the number of high value bits stored in a nondirect token.
    /// </summary>
    public uint MsbInToken { get; }

    /// <summary>
    /// Gets the number of low value bits stored in a nondirect token.
    /// </summary>
    public uint LsbInToken { get; }

    /// <summary>
    /// Gets the mask for the low value bits stored in the token.
    /// </summary>
    public uint LsbMask => (1u << (int)this.LsbInToken) - 1;

    /// <summary>
    /// Divides an unsigned value into its entropy token and remaining bit payload.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="token">The symbol to encode through the selected entropy table.</param>
    /// <param name="bitCount">The number of payload bits to write after the token.</param>
    /// <param name="bits">The payload bits in least-significant-bit-first order.</param>
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
                // Values below SplitToken are direct. Larger values remove their leading one, then place the exponent
                // and selected high/low value bits in the token. Only the intervening bits remain in the payload.
                token = this.SplitToken +
                (uint)(((n - this.SplitExponent) << (int)(this.MsbInToken + this.LsbInToken)) +
                        ((m >> (int)(n - this.MsbInToken)) << (int)this.LsbInToken) +
                        (m & ((1 << (int)this.LsbInToken) - 1)));

                bitCount = n - this.MsbInToken - this.LsbInToken;
                bits = (value >> (int)this.LsbInToken) & ((1u << (int)bitCount) - 1);
            }
        }
    }
}
