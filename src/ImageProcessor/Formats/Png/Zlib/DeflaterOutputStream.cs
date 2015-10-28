// <copyright file="DeflaterOutputStream.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// A special stream deflating or compressing the bytes that are
    /// written to it.  It uses a Deflater to perform actual deflating.<br/>
    /// Authors of the original java version : Tom Tromey, Jochen Hoenicke
    /// </summary>
    public class DeflaterOutputStream : Stream
    {
        /// <summary>
        /// The deflater which is used to deflate the stream.
        /// </summary>
        private readonly Deflater deflater;

        /// <summary>
        /// Base stream the deflater depends on.
        /// </summary>
        private readonly Stream baseOutputStream;

        /// <summary>
        /// This buffer is used temporarily to retrieve the bytes from the
        /// deflater and write them to the underlying output stream.
        /// </summary>
        private readonly byte[] bytebuffer;

        /// <summary>
        /// The password
        /// </summary>
        private string password;

        /// <summary>
        /// The keys
        /// </summary>
        private uint[] keys;

        /// <summary>
        /// Whether the stream is closed
        /// </summary>
        private bool isClosed;

        /// <summary>
        /// Whether dispose should close the underlying stream.
        /// </summary>
        private bool isStreamOwner = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterOutputStream"/> class
        /// with a default Deflater and default buffer size.
        /// </summary>
        /// <param name="baseOutputStream">
        /// the output stream where deflated output should be written.
        /// </param>
        public DeflaterOutputStream(Stream baseOutputStream)
            : this(baseOutputStream, new Deflater(), 512)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterOutputStream"/> class
        /// with the given Deflater and default buffer size.
        /// </summary>
        /// <param name="baseOutputStream">
        /// the output stream where deflated output should be written.
        /// </param>
        /// <param name="deflater">
        /// the underlying deflater.
        /// </param>
        public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater)
            : this(baseOutputStream, deflater, 512)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterOutputStream"/> class
        /// with the given Deflater and buffer size.
        /// </summary>
        /// <param name="baseOutputStream">
        /// The output stream where deflated output is written.
        /// </param>
        /// <param name="deflater">
        /// The underlying deflater to use
        /// </param>
        /// <param name="bufferSize">
        /// The buffer size in bytes to use when deflating (minimum value 512)
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">buffersize is less than or equal to zero.</exception>
        /// <exception cref="ArgumentException">baseOutputStream does not support writing.</exception>
        /// <exception cref="ArgumentNullException">deflater instance is null.</exception>
        public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater, int bufferSize)
        {
            if (baseOutputStream == null)
            {
                throw new ArgumentNullException(nameof(baseOutputStream));
            }

            if (baseOutputStream.CanWrite == false)
            {
                throw new ArgumentException("Must support writing", nameof(baseOutputStream));
            }

            if (deflater == null)
            {
                throw new ArgumentNullException(nameof(deflater));
            }

            if (bufferSize < 512)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            this.baseOutputStream = baseOutputStream;
            this.bytebuffer = new byte[bufferSize];
            this.deflater = deflater;
        }

        /// <summary>
        /// Get/set flag indicating ownership of the underlying stream.
        /// When the flag is true <see cref="Dispose"></see> will close the underlying stream also.
        /// </summary>
        public bool IsStreamOwner
        {
            get { return this.isStreamOwner; }
            set { this.isStreamOwner = value; }
        }

        /// <summary>
        /// Allows client to determine if an entry can be patched after its added
        /// </summary>
        public bool CanPatchEntries => this.baseOutputStream.CanSeek;

        /// <summary>
        /// Get/set the password used for encryption.
        /// </summary>
        /// <remarks>When set to null or if the password is empty no encryption is performed</remarks>
        public string Password
        {
            get
            {
                return this.password;
            }

            set
            {
                if ((value != null) && (value.Length == 0))
                {
                    this.password = null;
                }
                else
                {
                    this.password = value;
                }
            }
        }

        /// <summary>
        /// Gets value indicating stream can be read from
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// Gets a value indicating if seeking is supported for this stream
        /// This property always returns false
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Get value indicating if this stream supports writing
        /// </summary>
        public override bool CanWrite => this.baseOutputStream.CanWrite;

        /// <summary>
        /// Get current length of stream
        /// </summary>
        public override long Length => this.baseOutputStream.Length;

        /// <summary>
        /// Gets the current position within the stream.
        /// </summary>
        /// <exception cref="NotSupportedException">Any attempt to set position</exception>
        public override long Position
        {
            get
            {
                return this.baseOutputStream.Position;
            }

            set
            {
                throw new NotSupportedException("Position property not supported");
            }
        }

        /// <summary>
        /// Finishes the stream by calling finish() on the deflater.
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Not all input is deflated
        /// </exception>
        public virtual void Finish()
        {
            this.deflater.Finish();
            while (!this.deflater.IsFinished)
            {
                int len = this.deflater.Deflate(this.bytebuffer, 0, this.bytebuffer.Length);
                if (len <= 0)
                {
                    break;
                }

                if (this.keys != null)
                {
                    this.EncryptBlock(this.bytebuffer, 0, len);
                }

                this.baseOutputStream.Write(this.bytebuffer, 0, len);
            }

            if (!this.deflater.IsFinished)
            {
                throw new ImageFormatException("Can't deflate all input?");
            }

            this.baseOutputStream.Flush();
            this.keys = null;
        }

        /// <summary>
        /// Sets the current position of this stream to the given value. Not supported by this class!
        /// </summary>
        /// <param name="offset">The offset relative to the <paramref name="origin"/> to seek.</param>
        /// <param name="origin">The <see cref="SeekOrigin"/> to seek from.</param>
        /// <returns>The new position in the stream.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("DeflaterOutputStream Seek not supported");
        }

        /// <summary>
        /// Sets the length of this stream to the given value. Not supported by this class!
        /// </summary>
        /// <param name="value">The new stream length.</param>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("DeflaterOutputStream SetLength not supported");
        }

        /// <summary>
        /// Read a byte from stream advancing position by one
        /// </summary>
        /// <returns>The byte read cast to an int.  THe value is -1 if at the end of the stream.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override int ReadByte()
        {
            throw new NotSupportedException("DeflaterOutputStream ReadByte not supported");
        }

        /// <summary>
        /// Read a block of bytes from stream
        /// </summary>
        /// <param name="buffer">The buffer to store read data in.</param>
        /// <param name="offset">The offset to start storing at.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The actual number of bytes read.  Zero if end of stream is detected.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("DeflaterOutputStream Read not supported");
        }

        /// <summary>
        /// Flushes the stream by calling <see cref="DeflaterOutputStream.Flush">Flush</see> on the deflater and then
        /// on the underlying stream.  This ensures that all bytes are flushed.
        /// </summary>
        public override void Flush()
        {
            this.deflater.Flush();
            this.Deflate();
            this.baseOutputStream.Flush();
        }

        /// <summary>
        /// Writes a single byte to the compressed output stream.
        /// </summary>
        /// <param name="value">
        /// The byte value.
        /// </param>
        public override void WriteByte(byte value)
        {
            byte[] b = new byte[1];
            b[0] = value;
            this.Write(b, 0, 1);
        }

        /// <summary>
        /// Writes bytes from an array to the compressed stream.
        /// </summary>
        /// <param name="buffer">
        /// The byte array
        /// </param>
        /// <param name="offset">
        /// The offset into the byte array where to start.
        /// </param>
        /// <param name="count">
        /// The number of bytes to write.
        /// </param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.deflater.SetInput(buffer, offset, count);
            this.Deflate();
        }

        /// <summary>
        /// Encrypt a block of data
        /// </summary>
        /// <param name="buffer">
        /// Data to encrypt.  NOTE the original contents of the buffer are lost
        /// </param>
        /// <param name="offset">
        /// Offset of first byte in buffer to encrypt
        /// </param>
        /// <param name="length">
        /// Number of bytes in buffer to encrypt
        /// </param>
        protected void EncryptBlock(byte[] buffer, int offset, int length)
        {
            for (int i = offset; i < offset + length; ++i)
            {
                byte oldbyte = buffer[i];
                buffer[i] ^= this.EncryptByte();
                this.UpdateKeys(oldbyte);
            }
        }

        /// <summary>
        /// Initializes encryption keys based on given <paramref name="pssword"/>.
        /// </summary>
        /// <param name="pssword">The password.</param>
        protected void InitializePassword(string pssword)
        {
            this.keys = new uint[]
            {
                0x12345678,
                0x23456789,
                0x34567890
            };

            byte[] rawPassword = ZipConstants.ConvertToArray(pssword);

            foreach (byte b in rawPassword)
            {
                this.UpdateKeys(b);
            }
        }

        /// <summary>
        /// Encrypt a single byte
        /// </summary>
        /// <returns>
        /// The encrypted value
        /// </returns>
        protected byte EncryptByte()
        {
            uint temp = (this.keys[2] & 0xFFFF) | 2;
            return (byte)((temp * (temp ^ 1)) >> 8);
        }

        /// <summary>
        /// Update encryption keys
        /// </summary>
        /// <param name="ch">The character.</param>
        protected void UpdateKeys(byte ch)
        {
            this.keys[0] = Crc32.ComputeCrc32(this.keys[0], ch);
            this.keys[1] = this.keys[1] + (byte)this.keys[0];
            this.keys[1] = (this.keys[1] * 134775813) + 1;
            this.keys[2] = Crc32.ComputeCrc32(this.keys[2], (byte)(this.keys[1] >> 24));
        }

        /// <summary>
        /// Deflates everything in the input buffers.  This will call
        /// <code>def.deflate()</code> until all bytes from the input buffers
        /// are processed.
        /// </summary>
        protected void Deflate()
        {
            while (!this.deflater.IsNeedingInput)
            {
                int deflateCount = this.deflater.Deflate(this.bytebuffer, 0, this.bytebuffer.Length);

                if (deflateCount <= 0)
                {
                    break;
                }

                if (this.keys != null)
                {
                    this.EncryptBlock(this.bytebuffer, 0, deflateCount);
                }

                this.baseOutputStream.Write(this.bytebuffer, 0, deflateCount);
            }

            if (!this.deflater.IsNeedingInput)
            {
                throw new ImageFormatException("DeflaterOutputStream can't deflate all input?");
            }
        }

        /// <summary>
        /// Calls <see cref="Finish"/> and closes the underlying
        /// stream when <see cref="IsStreamOwner"></see> is true.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !this.isClosed)
            {
                this.isClosed = true;

                try
                {
                    this.Finish();
                    this.keys = null;
                }
                finally
                {
                    if (this.isStreamOwner)
                    {
                        this.baseOutputStream.Dispose();
                    }
                }
            }
        }
    }
}
