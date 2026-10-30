// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

internal static class JxlAnsEncoder
{
    /// <summary>
    /// Encodes a variable-length byte. The larger the number,
    /// the more bits it encodes, and the smaller the number,
    /// the lower bits it encodes, automatically. It looks
    /// like this:
    /// <code>
    /// is_nonzero_flag = Bits(1)
    /// if (is_nonzero_flag == 0) {
    ///   return 0;
    /// } else {
    ///   bits = Bits(3)
    ///   suffix = Bits(bits)
    ///   return suffix
    /// }
    /// </code>
    /// <para>
    /// This simple variable-length coding scheme encodes the
    /// structure into multiple bits. The first bit specifies
    /// whether the coefficient is NOT equal to 0. If this first
    /// bit is equal to 0, then that means the variable-length
    /// number value is 0, and therefore the result is immediately
    /// 0. This means 0 requires the lowest amount of bits; just
    /// an indicator that the value is equal to 0, which is equal
    /// to just 1 bit. However, if this value is equal to 1, that
    /// means the value is greater than 0. Following this, 3
    /// bits are read from the stream, which indicate the length
    /// of the suffix. This variable will be referred to as "nbits".
    /// Following that, the next "nbits" number of bits are read
    /// and is referred to as the suffix. Therefore, the result
    /// is (1 &lt;&lt; nbits) + suffix.
    /// </para>
    /// <para>
    /// As an example, to encode 20, we first encode a 1 bit, because
    /// our input is not equal to 0. Then we compute Floor(Log2(20)) to
    /// derive the number of suffix bits. We get 4 - the length of
    /// the suffix. This narrows the interval to (1 &lt;&lt; 4) .. ((1 &lt; &lt; (4 + 1)) - 1),
    /// meaning 16 to 31 can be encoded with this suffix length. Note
    /// that nbits adds (1 &lt;&lt; nbits) to the result automatically. So,
    /// subtract 1 &lt;&lt; nbits from the input value to get 4,
    /// and encode 0b0100 as the suffix.
    /// </para>
    /// </summary>
    /// <param name="n">
    /// The number to encode.
    /// </param>
    /// <param name="writer">
    /// The bit-writer where the number is encoded into.
    /// </param>
    /// <remarks>
    /// The byte must have a 0..255 range.
    /// </remarks>
    public static void StoreVarLenUInt8(int n, JxlBitWriter writer)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 255, nameof(n));

        if (n == 0)
        {
            writer.Write(1, 0);
        }
        else
        {
            writer.Write(1, 1);
            int nbits = JxlMath.FloorLog2Nonzero(n);
            writer.Write(3, nbits);
            writer.Write(nbits, n - (1 << nbits));
        }
    }

    /// <summary>
    /// Encodes a variable-length word. The larger the number,
    /// the more bits it encodes, and the smaller the number,
    /// the lower bits it encodes, automatically. It looks
    /// like this:
    /// <code>
    /// is_nonzero_flag = Bits(1)
    /// if (is_nonzero_flag == 0) {
    ///   return 0;
    /// } else {
    ///   bits = Bits(4)
    ///   suffix = Bits(bits)
    ///   return suffix
    /// }
    /// </code>
    /// <para>
    /// This simple variable-length coding scheme encodes the
    /// structure into multiple bits. The first bit specifies
    /// whether the coefficient is NOT equal to 0. If this first
    /// bit is equal to 0, then that means the variable-length
    /// number value is 0, and therefore the result is immediately
    /// 0. This means 0 requires the lowest amount of bits; just
    /// an indicator that the value is equal to 0, which is equal
    /// to just 1 bit. However, if this value is equal to 1, that
    /// means the value is greater than 0. Following this, 3
    /// bits are read from the stream, which indicate the length
    /// of the suffix. This variable will be referred to as "nbits".
    /// Following that, the next "nbits" number of bits are read
    /// and is referred to as the suffix. Therefore, the result
    /// is (1 &lt;&lt; nbits) + suffix.
    /// </para>
    /// <para>
    /// As an example, to encode 20, we first encode a 1 bit, because
    /// our input is not equal to 0. Then we compute Floor(Log2(20)) to
    /// derive the number of suffix bits. We get 4 - the length of
    /// the suffix. This narrows the interval to (1 &lt;&lt; 4) .. ((1 &lt; &lt; (4 + 1)) - 1),
    /// meaning 16 to 31 can be encoded with this suffix length. Note
    /// that nbits adds (1 &lt;&lt; nbits) to the result automatically. So,
    /// subtract 1 &lt;&lt; nbits from the input value to get 4,
    /// and encode 0b0100 as the suffix.
    /// </para>
    /// </summary>
    /// <param name="n">
    /// The number to encode.
    /// </param>
    /// <param name="writer">
    /// The bit-writer where the number is encoded into.
    /// </param>
    /// <remarks>
    /// The byte must have a 0..65535 range.
    /// </remarks>
    public static void StoreVarLenUInt16(int n, JxlBitWriter writer)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 65535, nameof(n));

        if (n == 0)
        {
            writer.Write(1, 0);
        }
        else
        {
            writer.Write(1, 1);
            int nbits = JxlMath.FloorLog2Nonzero(n);
            writer.Write(4, nbits);
            writer.Write(nbits, n - (1 << nbits));
        }
    }

    /// <summary>
    /// Encodes an ANS Hybrid uint config.
    /// </summary>
    /// <param name="uintConfig">The hybrid uint config to be encoded.</param>
    /// <param name="writer">Bit-stream where the configuration will be written to.</param>
    /// <param name="logAlphaSize">For MSB/LSB</param>
    public static void EncodeUIntConfiguration(JxlAnsHybridUIntConfiguration uintConfig, JxlBitWriter writer, int logAlphaSize)
    {
        writer.Write(JxlMath.CeilLog2Nonzero(logAlphaSize + 1), uintConfig.SplitExponent);

        if (uintConfig.SplitExponent == logAlphaSize)
        {
            // The MSB/LSB do not matter
            return;
        }

        int numBits = (int)JxlMath.CeilLog2Nonzero(uintConfig.SplitExponent + 1);
        writer.Write(numBits, uintConfig.MsbInToken);

        numBits = (int)JxlMath.CeilLog2Nonzero(uintConfig.SplitExponent - uintConfig.MsbInToken + 1);
        writer.Write(numBits, uintConfig.LsbInToken);
    }

    /// <summary>
    /// Encodes multiple hybrid UInt configurations.
    /// </summary>
    /// <param name="configs">The configurations to encode.</param>
    /// <param name="writer">Bit-stream where the configurations are written to.</param>
    /// <param name="logAlphaSize">For MSB/LSB.</param>
    public static void EncodeUIntConfigurations(Span<JxlAnsHybridUIntConfiguration> configs, JxlBitWriter writer, int logAlphaSize)
    {
        foreach (JxlAnsHybridUIntConfiguration config in configs)
        {
            EncodeUIntConfiguration(config, writer, logAlphaSize);
        }
    }
}
