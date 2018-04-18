// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Interface to compute a data checksum used by checked input/output streams.
    /// A data checksum can be updated by one byte or with a byte array. After each
    /// update the value of the current checksum can be returned by calling
    /// <code>Value</code>. The complete checksum object can also be reset
    /// so it can be used again with new data.
    /// </summary>
    internal interface IChecksum
    {
        /// <summary>
        /// Gets the data checksum computed so far.
        /// </summary>
        long Value { get; }

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
        /// Updates the data checksum with the bytes taken from the span.
        /// </summary>
        /// <param name="data">
        /// buffer an array of bytes
        /// </param>
        void Update(ReadOnlySpan<byte> data);
    }
}
