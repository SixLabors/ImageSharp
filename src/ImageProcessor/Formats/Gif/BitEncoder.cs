// <copyright file="BitEncoder.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Handles the encoding of bits for compression.
    /// </summary>
    internal class BitEncoder
    {
        /// <summary>
        /// The inner list for collecting the bits.
        /// </summary>
        private readonly List<byte> list = new List<byte>();

        /// <summary>
        /// The current working bit.
        /// </summary>
        private int currentBit;

        /// <summary>
        /// The current value.
        /// </summary>
        private int currentValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitEncoder"/> class.
        /// </summary>
        /// <param name="initial">
        /// The initial bits.
        /// </param>
        public BitEncoder(int initial)
        {
            this.IntitialBit = initial;
        }

        /// <summary>
        /// Gets or sets the intitial bit.
        /// </summary>
        public int IntitialBit { get; set; }

        /// <summary>
        /// The number of bytes in the encoder.
        /// </summary>
        public int Length => this.list.Count;

        /// <summary>
        /// Adds the current byte to the end of the encoder.
        /// </summary>
        /// <param name="item">
        /// The byte to add.
        /// </param>
        public void Add(int item)
        {
            this.currentValue |= item << this.currentBit;

            this.currentBit += this.IntitialBit;

            while (this.currentBit >= 8)
            {
                byte value = (byte)(this.currentValue & 0XFF);
                this.currentValue = this.currentValue >> 8;
                this.currentBit -= 8;
                this.list.Add(value);
            }
        }

        /// <summary>
        /// Adds the collection of bytes to the end of the encoder.
        /// </summary>
        /// <param name="collection">
        /// The collection of bytes to add.
        /// The collection itself cannot be null but can contain elements that are null.</param>
        public void AddRange(byte[] collection)
        {
            this.list.AddRange(collection);
        }

        /// <summary>
        /// The end.
        /// </summary>
        internal void End()
        {
            while (this.currentBit > 0)
            {
                byte value = (byte)(this.currentValue & 0XFF);
                this.currentValue = this.currentValue >> 8;
                this.currentBit -= 8;
                this.list.Add(value);
            }
        }

        /// <summary>
        /// Copies a range of elements from the encoder to a compatible one-dimensional array, 
        /// starting at the specified index of the target array.
        /// </summary>
        /// <param name="index">
        /// The zero-based index in the source <see cref="BitEncoder"/> at which copying begins.
        /// </param>
        /// <param name="array">
        /// The one-dimensional Array that is the destination of the elements copied 
        /// from <see cref="BitEncoder"/>. The Array must have zero-based indexing
        /// </param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        /// <param name="count">The number of bytes to copy.</param>
        public void CopyTo(int index, byte[] array, int arrayIndex, int count)
        {
            this.list.CopyTo(index, array, arrayIndex, count);
        }

        /// <summary>
        /// Removes all the bytes from the encoder.
        /// </summary>
        public void Clear()
        {
            this.list.Clear();
        }

        /// <summary>
        /// Copies the bytes into a new array.
        /// </summary>
        /// <returns><see cref="T:byte[]"/></returns>
        public byte[] ToArray()
        {
            return this.list.ToArray();
        }
    }
}
