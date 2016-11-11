// <copyright file="Huffman.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Buffers;

namespace ImageSharp.Formats
{
    /// <summary>
    /// Represents a Huffman tree
    /// </summary>
    internal struct Huffman : IDisposable
    {
        private static ArrayPool<ushort> UshortBuffer =
            //ArrayPool<ushort>.Shared;
            ArrayPool<ushort>.Create(1 << JpegDecoderCore.LutSize, 50);

        private static ArrayPool<byte> ByteBuffer = 
            //ArrayPool<byte>.Shared;
            ArrayPool<byte>.Create(JpegDecoderCore.MaxNCodes, 50);

        private static readonly ArrayPool<int> IntBuffer =
            //ArrayPool<int>.Shared;
            ArrayPool<int>.Create(JpegDecoderCore.MaxCodeLength, 50);

        public void Init(int lutSize, int maxNCodes, int maxCodeLength)
        {
            //this.Lut = new ushort[1 << lutSize];
            //this.Values = new byte[maxNCodes];
            //this.MinCodes = new int[maxCodeLength];
            //this.MaxCodes = new int[maxCodeLength];
            //this.Indices = new int[maxCodeLength];

            this.Lut = UshortBuffer.Rent(1 << lutSize);
            this.Values = ByteBuffer.Rent(maxNCodes);
            this.MinCodes = IntBuffer.Rent(maxCodeLength);
            this.MaxCodes = IntBuffer.Rent(maxCodeLength); ;
            this.Indices = IntBuffer.Rent(maxCodeLength); ;
        }

        /// <summary>
        /// Gets or sets the number of codes in the tree.
        /// </summary>
        public int Length;

        /// <summary>
        /// Gets the look-up table for the next LutSize bits in the bit-stream.
        /// The high 8 bits of the uint16 are the encoded value. The low 8 bits
        /// are 1 plus the code length, or 0 if the value is too large to fit in
        /// lutSize bits.
        /// </summary>
        public ushort[] Lut;

        /// <summary>
        /// Gets the the decoded values, sorted by their encoding.
        /// </summary>
        public byte[] Values;

        /// <summary>
        /// Gets the array of minimum codes.
        /// MinCodes[i] is the minimum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public int[] MinCodes;

        /// <summary>
        /// Gets the array of maximum codes.
        /// MaxCodes[i] is the maximum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public int[] MaxCodes;

        /// <summary>
        /// Gets the array of indices. Indices[i] is the index into Values of MinCodes[i].
        /// </summary>
        public int[] Indices;

        public void Dispose()
        {
            UshortBuffer.Return(Lut, true);
            ByteBuffer.Return(Values, true);
            IntBuffer.Return(MinCodes, true);
            IntBuffer.Return(MaxCodes, true);
            IntBuffer.Return(Indices, true);
        }
    }

    
}