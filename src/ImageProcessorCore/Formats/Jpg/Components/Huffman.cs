// <copyright file="Huffman.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Represents a Huffman tree
    /// </summary>
    internal class Huffman
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Huffman"/> class. 
        /// </summary>
        /// <param name="lutSize">The log-2 size of the Huffman decoder's look-up table.</param>
        /// <param name="maxNCodes">The maximum (inclusive) number of codes in a Huffman tree.</param>
        /// <param name="maxCodeLength">The maximum (inclusive) number of bits in a Huffman code.</param>
        public Huffman(int lutSize, int maxNCodes, int maxCodeLength)
        {
            this.Lut = new ushort[1 << lutSize];
            this.Values = new byte[maxNCodes];
            this.MinCodes = new int[maxCodeLength];
            this.MaxCodes = new int[maxCodeLength];
            this.Indices = new int[maxCodeLength];
            this.Length = 0;
        }

        /// <summary>
        /// Gets or sets the number of codes in the tree.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Gets the look-up table for the next LutSize bits in the bit-stream.
        /// The high 8 bits of the uint16 are the encoded value. The low 8 bits
        /// are 1 plus the code length, or 0 if the value is too large to fit in
        /// lutSize bits.
        /// </summary>
        public ushort[] Lut { get; }

        /// <summary>
        /// Gets the the decoded values, sorted by their encoding.
        /// </summary>
        public byte[] Values { get; }

        /// <summary>
        /// Gets the array of minimum codes.
        /// MinCodes[i] is the minimum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public int[] MinCodes { get; }

        /// <summary>
        /// Gets the array of maximum codes.
        /// MaxCodes[i] is the maximum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public int[] MaxCodes { get; }

        /// <summary>
        /// Gets the array of indices. Indices[i] is the index into Values of MinCodes[i].
        /// </summary>
        public int[] Indices { get; }
    }
}