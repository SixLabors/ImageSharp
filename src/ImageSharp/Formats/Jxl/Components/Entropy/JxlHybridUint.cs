// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;

/// <summary>
/// Splits an unsigned integer between an entropy token and following literal bits.
/// </summary>
/// <remarks>
/// Read validates all configurations used by general expansion. Default values are retained only for absent LZ77
/// copy-length data or ignored fixed-width operator arguments; they do not represent a parsed configuration.
/// </remarks>
internal readonly struct JxlHybridUint
{
    // Prefix alphabets have at most 15 bits; ANS alphabets and copy-length configurations have at most eight.
    // Read bounds each width by that alphabet before construction. Three byte widths and a cached uint token
    // occupy eight bytes with natural alignment. Cache the split once rather than shifting for every decoded value.
    private readonly uint splitToken;
    private readonly byte splitExponent;
    private readonly byte highBits;
    private readonly byte lowBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlHybridUint"/> struct from validated field widths.
    /// </summary>
    /// <param name="splitExponent">The threshold exponent.</param>
    /// <param name="highBits">The leading bits retained in the token.</param>
    /// <param name="lowBits">The trailing bits retained in the token.</param>
    private JxlHybridUint(int splitExponent, int highBits, int lowBits)
    {
        this.splitToken = 1U << splitExponent;
        this.splitExponent = (byte)splitExponent;
        this.highBits = (byte)highBits;
        this.lowBits = (byte)lowBits;
    }

    /// <summary>
    /// Gets the configuration that stores zero directly and splits larger values at their leading bit.
    /// </summary>
    public static JxlHybridUint Zero { get; } = new(0, 0, 0);

    /// <summary>
    /// Gets a value indicating whether this configuration uses a four-bit split, two leading token bits and no trailing token bits.
    /// </summary>
    public bool Is420 => this.splitExponent == 4 && this.highBits == 2 && this.lowBits == 0;

    /// <summary>
    /// Gets the conservative output width represented by an entropy token without consuming its middle bits.
    /// </summary>
    /// <param name="token">The validated table symbol, adjusted for copy-length syntax when required.</param>
    /// <returns>The configured split width for a direct token, or the reconstructed width for an expanded token.</returns>
    public int GetBitCount(uint token)
    {
        return token < this.splitToken
            ? this.splitExponent
            : this.splitExponent + 1 + (int)((token - this.splitToken) >> (this.highBits + this.lowBits));
    }

    /// <summary>
    /// Reads and validates one hybrid integer configuration.
    /// </summary>
    /// <param name="reader">The bounded bit reader.</param>
    /// <param name="alphabetBits">The entropy alphabet's maximum bit width.</param>
    /// <returns>The validated configuration.</returns>
    public static JxlHybridUint Read(ref JxlBitReader reader, int alphabetBits)
    {
        // A split at the alphabet width encodes values directly. Smaller splits also describe
        // leading and trailing token bits; their combined width cannot exceed the split exponent.
        int split = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)alphabetBits));
        if (split > alphabetBits)
        {
            throw new InvalidImageContentException("The JPEG XL hybrid integer split exceeds its alphabet.");
        }

        int high = 0;
        int low = 0;
        if (split != alphabetBits)
        {
            high = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)split));
            if (high > split)
            {
                throw new InvalidImageContentException("The JPEG XL hybrid integer leading width is invalid.");
            }

            low = (int)reader.ReadBits(32 - BitOperations.LeadingZeroCount((uint)(split - high)));
            if (high + low > split)
            {
                throw new InvalidImageContentException("The JPEG XL hybrid integer trailing width is invalid.");
            }
        }

        return new JxlHybridUint(split, high, low);
    }

    /// <summary>
    /// Splits one unsigned value into an entropy token and following literal bits.
    /// </summary>
    /// <param name="value">The unsigned value to encode.</param>
    /// <param name="token">The entropy-coded token.</param>
    /// <param name="bitCount">The number of following literal bits.</param>
    /// <param name="extraBits">The literal middle bits, with all higher bits clear.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Encode(uint value, out uint token, out int bitCount, out uint extraBits)
    {
        if (this.splitExponent == 0)
        {
            // A zero split also has zero leading and trailing widths. Log2(0) is zero, so direct zero
            // and expanded values can share the same arithmetic. Only a nonzero value adds the implicit-one token.
            // This avoids a data-dependent zero branch for the encoder's fixed Zero configuration.
            bitCount = BitOperations.Log2(value);
            token = (uint)bitCount + (value == 0 ? 0U : 1U);
            extraBits = value & ((1U << bitCount) - 1);
            return;
        }

        if (value < this.splitToken)
        {
            token = value;
            bitCount = 0;
            extraBits = 0;
            return;
        }

        // Remove the implicit leading one. The token carries its position,
        // the configured leading value bits and the trailing value bits. Only the middle bits follow the token.
        // Parsed widths satisfy highBits + lowBits <= splitExponent, so every shift below is within 0..31.
        // Mask the shifted value to remove the leading one. A zero leading width then folds to zero
        // without subtracting and shifting a remainder for every encoded sample.
        int exponent = BitOperations.Log2(value);
        token = this.splitToken
            + ((uint)(exponent - this.splitExponent) << (this.highBits + this.lowBits))
            + (((value >> (exponent - this.highBits)) & ((1U << this.highBits) - 1)) << this.lowBits)
            + (value & ((1U << this.lowBits) - 1));

        bitCount = exponent - this.highBits - this.lowBits;
        extraBits = (value >> this.lowBits) & ((1U << bitCount) - 1);
    }

    /// <summary>
    /// Reconstructs one unsigned value without allowing malformed widths to wrap a machine shift.
    /// </summary>
    /// <param name="reader">The bounded bit reader.</param>
    /// <param name="token">The entropy-coded token.</param>
    /// <returns>The reconstructed unsigned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Expand(ref JxlBitReader reader, uint token)
    {
        if (token < this.splitToken)
        {
            return token;
        }

        // Above the direct-value range, the token stores a length index plus leading and trailing
        // value bits. Insert the implicit leading one and read only the middle bits from the stream.
        // Validate the reconstructed width first because C# masks an out-of-range shift count.
        int tokenBits = this.highBits + this.lowBits;
        uint bits = (uint)(this.splitExponent - tokenBits) + ((token - this.splitToken) >> tokenBits);
        if (bits + (uint)tokenBits >= 32)
        {
            throw new InvalidImageContentException("The JPEG XL hybrid integer exceeds 32 bits.");
        }

        uint low = token & ((1U << this.lowBits) - 1);
        uint high = ((token >> this.lowBits) & ((1U << this.highBits) - 1)) | (1U << this.highBits);
        return (((high << (int)bits) | reader.ReadBits((int)bits)) << this.lowBits) | low;
    }
}
