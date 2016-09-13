// <copyright file="Bits.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Holds the unprocessed bits that have been taken from the byte-stream.
    /// The n least significant bits of a form the unread bits, to be read in MSB to
    /// LSB order.
    /// </summary>
    internal class Bits
    {
        /// <summary>
        /// Gets or sets the accumulator.
        /// </summary>
        public uint Accumulator { get; set; }

        /// <summary>
        /// Gets or sets the mask. 
        /// <![CDATA[mask==1<<(unreadbits-1) when unreadbits>0, with mask==0 when unreadbits==0.]]>
        /// </summary>
        public uint Mask { get; set; }

        /// <summary>
        /// Gets or sets the  number of unread bits in the accumulator.
        /// </summary>
        public int UnreadBits { get; set; }
    }
}