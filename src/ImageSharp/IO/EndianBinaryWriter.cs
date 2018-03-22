// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// Equivalent of <see cref="BinaryWriter"/>, but with either endianness
    /// </summary>
    internal class EndianBinaryWriter : IDisposable
    {
        /// <summary>
        /// Buffer used for temporary storage during conversion from primitives
        /// </summary>
        private readonly byte[] buffer = new byte[16];

        /// <summary>
        /// The endianness used to write the data
        /// </summary>
        private readonly Endianness endianness;

        /// <summary>
        /// Whether or not this writer has been disposed yet.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EndianBinaryWriter"/> class
        /// with the given bit converter, writing to the given stream, using the given encoding.
        /// </summary>
        /// <param name="endianness">Endianness to use when writing data</param>
        /// <param name="stream">Stream to write data to</param>
        public EndianBinaryWriter(Endianness endianness, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.IsTrue(stream.CanWrite, nameof(stream), "Stream isn't writable");

            this.BaseStream = stream;
            this.endianness = endianness;
        }

        /// <summary>
        /// Gets the underlying stream of the EndianBinaryWriter.
        /// </summary>
        public Stream BaseStream { get; }

        /// <summary>
        /// Closes the writer, including the underlying stream.
        /// </summary>
        public void Close()
        {
            this.Dispose();
        }

        /// <summary>
        /// Flushes the underlying stream.
        /// </summary>
        public void Flush()
        {
            this.CheckDisposed();
            this.BaseStream.Flush();
        }

        /// <summary>
        /// Seeks within the stream.
        /// </summary>
        /// <param name="offset">Offset to seek to.</param>
        /// <param name="origin">Origin of seek operation.</param>
        public void Seek(int offset, SeekOrigin origin)
        {
            this.CheckDisposed();
            this.BaseStream.Seek(offset, origin);
        }

        /// <summary>
        /// Writes a boolean value to the stream. 1 byte is written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(bool value)
        {
            this.buffer[0] = value ? (byte)1 : (byte)0;

            this.WriteInternal(this.buffer, 1);
        }

        /// <summary>
        /// Writes a 16-bit signed integer to the stream, using the bit converter
        /// for this writer. 2 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(short value)
        {
            if (this.endianness == Endianness.BigEndian)
            {
                BinaryPrimitives.WriteInt16BigEndian(this.buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(this.buffer, value);
            }

            this.WriteInternal(this.buffer, 2);
        }

        /// <summary>
        /// Writes a 32-bit signed integer to the stream, using the bit converter
        /// for this writer. 4 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(int value)
        {
            if (this.endianness == Endianness.BigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(this.buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(this.buffer, value);
            }

            this.WriteInternal(this.buffer, 4);
        }

        /// <summary>
        /// Writes a 64-bit signed integer to the stream, using the bit converter
        /// for this writer. 8 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(long value)
        {
            if (this.endianness == Endianness.BigEndian)
            {
                BinaryPrimitives.WriteInt64BigEndian(this.buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteInt64LittleEndian(this.buffer, value);
            }

            this.WriteInternal(this.buffer, 8);
        }

        /// <summary>
        /// Writes a 16-bit unsigned integer to the stream, using the bit converter
        /// for this writer. 2 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(ushort value)
        {
            if (this.endianness == Endianness.BigEndian)
            {
                BinaryPrimitives.WriteUInt16BigEndian(this.buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(this.buffer, value);
            }

            this.WriteInternal(this.buffer, 2);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer to the stream, using the bit converter
        /// for this writer. 4 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(uint value)
        {
            if (this.endianness == Endianness.BigEndian)
            {
                BinaryPrimitives.WriteUInt32BigEndian(this.buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, value);
            }

            this.WriteInternal(this.buffer, 4);
        }

        /// <summary>
        /// Writes a 64-bit unsigned integer to the stream, using the bit converter
        /// for this writer. 8 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(ulong value)
        {
            if (this.endianness == Endianness.BigEndian)
            {
                BinaryPrimitives.WriteUInt64BigEndian(this.buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(this.buffer, value);
            }

            this.WriteInternal(this.buffer, 8);
        }

        /// <summary>
        /// Writes a single-precision floating-point value to the stream, using the bit converter
        /// for this writer. 4 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public unsafe void Write(float value)
        {
            this.Write(*((int*)&value));
        }

        /// <summary>
        /// Writes a double-precision floating-point value to the stream, using the bit converter
        /// for this writer. 8 bytes are written.
        /// </summary>
        /// <param name="value">The value to write</param>
        public unsafe void Write(double value)
        {
            this.Write(*((long*)&value));
        }

        /// <summary>
        /// Writes a signed byte to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(byte value)
        {
            this.buffer[0] = value;
            this.WriteInternal(this.buffer, 1);
        }

        /// <summary>
        /// Writes an unsigned byte to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(sbyte value)
        {
            this.buffer[0] = unchecked((byte)value);
            this.WriteInternal(this.buffer, 1);
        }

        /// <summary>
        /// Writes an array of bytes to the stream.
        /// </summary>
        /// <param name="value">The values to write</param>
        /// <exception cref="ArgumentNullException">value is null</exception>
        public void Write(byte[] value)
        {
            Guard.NotNull(value, nameof(value));
            this.WriteInternal(value, value.Length);
        }

        /// <summary>
        /// Writes a portion of an array of bytes to the stream.
        /// </summary>
        /// <param name="value">An array containing the bytes to write</param>
        /// <param name="offset">The index of the first byte to write within the array</param>
        /// <param name="count">The number of bytes to write</param>
        /// <exception cref="ArgumentNullException">value is null</exception>
        public void Write(byte[] value, int offset, int count)
        {
            this.CheckDisposed();
            this.BaseStream.Write(value, offset, count);
        }

        /// <summary>
        /// Disposes of the underlying stream.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.Flush();
                this.disposed = true;
                ((IDisposable)this.BaseStream).Dispose();
            }
        }

        /// <summary>
        /// Checks whether or not the writer has been disposed, throwing an exception if so.
        /// </summary>
        private void CheckDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(EndianBinaryWriter));
            }
        }

        /// <summary>
        /// Writes the specified number of bytes from the start of the given byte array,
        /// after checking whether or not the writer has been disposed.
        /// </summary>
        /// <param name="bytes">The array of bytes to write from</param>
        /// <param name="length">The number of bytes to write</param>
        private void WriteInternal(byte[] bytes, int length)
        {
            this.CheckDisposed();
            this.BaseStream.Write(bytes, 0, length);
        }
    }
}