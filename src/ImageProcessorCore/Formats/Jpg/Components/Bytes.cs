// <copyright file="Bytes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Bytes is a byte buffer, similar to a stream, except that it
    /// has to be able to unread more than 1 byte, due to byte stuffing.
    /// Byte stuffing is specified in section F.1.2.3.
    /// </summary>
    internal class Bytes
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bytes"/> class.
        /// </summary>
        public Bytes()
        {
            this.Buffer = new byte[4096];
            this.I = 0;
            this.J = 0;
            this.UnreadableBytes = 0;
        }

        /// <summary>
        /// Gets or sets the buffer.
        /// buffer[i:j] are the buffered bytes read from the underlying
        /// stream that haven't yet been passed further on.
        /// </summary>
        public byte[] Buffer { get; set; }

        public int I { get; set; }

        public int J { get; set; }

        /// <summary>
        /// Gets or sets the unreadable bytes. The number of bytes to back up i after
        /// overshooting. It can be 0, 1 or 2.
        /// </summary>
        public int UnreadableBytes { get; set; }
    }
}
