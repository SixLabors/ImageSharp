// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// A compiled look-up table representation of a huffmanSpec.
    /// The maximum codeword size is 16 bits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each value maps to a int32 of which the 24 most significant bits hold the
    /// codeword in bits and the 8 least significant bits hold the codeword size.
    /// </para>
    /// <para>
    /// Code value occupies 24 most significant bits as integer value.
    /// This value is shifted to the MSB position for performance reasons.
    /// For example, decimal value 10 is stored like this:
    /// <code>
    /// MSB                                LSB
    /// 1010 0000 00000000 00000000 | 00000100
    /// </code>
    /// This was done to eliminate extra binary shifts in the encoder.
    /// While code length is represented as 8 bit integer value
    /// </para>
    /// </remarks>
    internal readonly struct HuffmanLut
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanLut"/> struct.
        /// </summary>
        /// <param name="spec">dasd</param>
        public HuffmanLut(HuffmanSpec spec)
        {
            int maxValue = 0;

            foreach (byte v in spec.Values)
            {
                if (v > maxValue)
                {
                    maxValue = v;
                }
            }

            this.Values = new int[maxValue + 1];

            int code = 0;
            int k = 0;

            for (int i = 0; i < spec.Count.Length; i++)
            {
                int len = i + 1;
                for (int j = 0; j < spec.Count[i]; j++)
                {
                    this.Values[spec.Values[k]] = len | (code << (32 - len));
                    code++;
                    k++;
                }

                code <<= 1;
            }
        }

        /// <summary>
        /// Gets the collection of huffman values.
        /// </summary>
        public int[] Values { get; }
    }
}
