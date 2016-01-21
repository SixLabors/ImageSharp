// <copyright file="IChecksum.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Interface to compute a data checksum used by checked input/output streams.
    /// A data checksum can be updated by one byte or with a byte array. After each
    /// update the value of the current checksum can be returned by calling
    /// <code>getValue</code>. The complete checksum object can also be reset
    /// so it can be used again with new data.
    /// </summary>
    public interface IChecksum
    {
        /// <summary>
        /// Gets the data checksum computed so far.
        /// </summary>
        long Value
        {
            get;
        }

        /// <summary>
        /// Resets the data checksum as if no update was ever called.
        /// </summary>
        void Reset();

        /// <summary>
        /// Adds one byte to the data checksum.
        /// </summary>
        /// <param name = "value">
        /// The data value to add. The high byte of the integer is ignored.
        /// </param>
        void Update(int value);

        /// <summary>
        /// Updates the data checksum with the bytes taken from the array.
        /// </summary>
        /// <param name="buffer">
        /// buffer an array of bytes
        /// </param>
        void Update(byte[] buffer);

        /// <summary>
        /// Adds the byte array to the data checksum.
        /// </summary>
        /// <param name = "buffer">
        /// The buffer which contains the data
        /// </param>
        /// <param name = "offset">
        /// The offset in the buffer where the data starts
        /// </param>
        /// <param name = "count">
        /// the number of data bytes to add.
        /// </param>
        void Update(byte[] buffer, int offset, int count);
    }
}
