namespace ImageSharp.Formats.Jpg.Components
{
    /// <summary>
    /// A compiled look-up table representation of a huffmanSpec.
    /// Each value maps to a uint32 of which the 8 most significant bits hold the
    /// codeword size in bits and the 24 least significant bits hold the codeword.
    /// The maximum codeword size is 16 bits.
    /// </summary>
    internal struct HuffmanLut
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanLut"/> class.
        /// </summary>
        /// <param name="spec">The encoding specifications.</param>
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

            this.Values = new uint[maxValue + 1];

            int code = 0;
            int k = 0;

            for (int i = 0; i < spec.Count.Length; i++)
            {
                int bits = (i + 1) << 24;
                for (int j = 0; j < spec.Count[i]; j++)
                {
                    this.Values[spec.Values[k]] = (uint)(bits | code);
                    code++;
                    k++;
                }

                code <<= 1;
            }
        }

        /// <summary>
        /// Initialize static members
        /// </summary>
        static HuffmanLut()
        {
            // Initialize the Huffman tables
            for (int i = 0; i < HuffmanSpec.TheHuffmanSpecs.Length; i++)
            {
                HuffmanLut.TheHuffmanLut[i] = new HuffmanLut(HuffmanSpec.TheHuffmanSpecs[i]);
            }
        }

        /// <summary>
        /// Gets the collection of huffman values.
        /// </summary>
        public uint[] Values { get; }

        /// <summary>
        /// The compiled representations of theHuffmanSpec.
        /// </summary>
        public static readonly HuffmanLut[] TheHuffmanLut = new HuffmanLut[4];
    }
}