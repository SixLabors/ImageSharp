// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// Equivalent of <see cref="BinaryReader"/>, but with either endianness.
    /// No data is buffered in the reader; the client may seek within the stream at will.
    /// </summary>
    internal class EndianBinaryReader : IDisposable
    {
        /// <summary>
        /// Buffer used for temporary storage before conversion into primitives
        /// </summary>
        private readonly byte[] storageBuffer = new byte[16];

        /// <summary>
        /// Whether or not this reader has been disposed yet.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// The endianness used to read data
        /// </summary>
        private readonly Endianness endianness;

        /// <summary>
        /// Initializes a new instance of the <see cref="EndianBinaryReader"/> class.
        /// Constructs a new binary reader with the given bit converter, reading
        /// to the given stream, using the given encoding.
        /// </summary>
        /// <param name="endianness">Endianness to use when reading data</param>
        /// <param name="stream">Stream to read data from</param>
        public EndianBinaryReader(Endianness endianness, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.IsTrue(stream.CanRead, nameof(stream), "Stream isn't readable");

            this.BaseStream = stream;
            this.endianness = endianness;
        }

        /// <summary>
        /// Gets the underlying stream of the EndianBinaryReader.
        /// </summary>
        public Stream BaseStream { get; }

        /// <summary>
        /// Closes the reader, including the underlying stream.
        /// </summary>
        public void Close()
        {
            this.Dispose();
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
        /// Reads a single byte from the stream.
        /// </summary>
        /// <returns>The byte read</returns>
        public byte ReadByte()
        {
            this.ReadInternal(this.storageBuffer, 1);
            return this.storageBuffer[0];
        }

        /// <summary>
        /// Reads a single signed byte from the stream.
        /// </summary>
        /// <returns>The byte read</returns>
        public sbyte ReadSByte()
        {
            this.ReadInternal(this.storageBuffer, 1);
            return unchecked((sbyte)this.storageBuffer[0]);
        }

        /// <summary>
        /// Reads a boolean from the stream. 1 byte is read.
        /// </summary>
        /// <returns>The boolean read</returns>
        public bool ReadBoolean()
        {
            this.ReadInternal(this.storageBuffer, 1);

            return this.storageBuffer[0] != 0;
        }

        /// <summary>
        /// Reads a 16-bit signed integer from the stream, using the bit converter
        /// for this reader. 2 bytes are read.
        /// </summary>
        /// <returns>The 16-bit integer read</returns>
        public short ReadInt16()
        {
            this.ReadInternal(this.storageBuffer, 2);

            return (this.endianness == Endianness.BigEndian)
                ? BinaryPrimitives.ReadInt16BigEndian(this.storageBuffer)
                : BinaryPrimitives.ReadInt16LittleEndian(this.storageBuffer);
        }

        /// <summary>
        /// Reads a 32-bit signed integer from the stream, using the bit converter
        /// for this reader. 4 bytes are read.
        /// </summary>
        /// <returns>The 32-bit integer read</returns>
        public int ReadInt32()
        {
            this.ReadInternal(this.storageBuffer, 4);

            return (this.endianness == Endianness.BigEndian)
               ? BinaryPrimitives.ReadInt32BigEndian(this.storageBuffer)
               : BinaryPrimitives.ReadInt32LittleEndian(this.storageBuffer);
        }

        /// <summary>
        /// Reads a 64-bit signed integer from the stream, using the bit converter
        /// for this reader. 8 bytes are read.
        /// </summary>
        /// <returns>The 64-bit integer read</returns>
        public long ReadInt64()
        {
            this.ReadInternal(this.storageBuffer, 8);

            return (this.endianness == Endianness.BigEndian)
               ? BinaryPrimitives.ReadInt64BigEndian(this.storageBuffer)
               : BinaryPrimitives.ReadInt64LittleEndian(this.storageBuffer);
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer from the stream, using the bit converter
        /// for this reader. 2 bytes are read.
        /// </summary>
        /// <returns>The 16-bit unsigned integer read</returns>
        public ushort ReadUInt16()
        {
            this.ReadInternal(this.storageBuffer, 2);

            return (this.endianness == Endianness.BigEndian)
               ? BinaryPrimitives.ReadUInt16BigEndian(this.storageBuffer)
               : BinaryPrimitives.ReadUInt16LittleEndian(this.storageBuffer);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from the stream, using the bit converter
        /// for this reader. 4 bytes are read.
        /// </summary>
        /// <returns>The 32-bit unsigned integer read</returns>
        public uint ReadUInt32()
        {
            this.ReadInternal(this.storageBuffer, 4);

            return (this.endianness == Endianness.BigEndian)
               ? BinaryPrimitives.ReadUInt32BigEndian(this.storageBuffer)
               : BinaryPrimitives.ReadUInt32LittleEndian(this.storageBuffer);
        }

        /// <summary>
        /// Reads a 64-bit unsigned integer from the stream, using the bit converter
        /// for this reader. 8 bytes are read.
        /// </summary>
        /// <returns>The 64-bit unsigned integer read</returns>
        public ulong ReadUInt64()
        {
            this.ReadInternal(this.storageBuffer, 8);

            return (this.endianness == Endianness.BigEndian)
               ? BinaryPrimitives.ReadUInt64BigEndian(this.storageBuffer)
               : BinaryPrimitives.ReadUInt64LittleEndian(this.storageBuffer);
        }

        /// <summary>
        /// Reads a single-precision floating-point value from the stream, using the bit converter
        /// for this reader. 4 bytes are read.
        /// </summary>
        /// <returns>The floating point value read</returns>
        public unsafe float ReadSingle()
        {
            int intValue = this.ReadInt32();

            return *((float*)&intValue);
        }

        /// <summary>
        /// Reads a double-precision floating-point value from the stream, using the bit converter
        /// for this reader. 8 bytes are read.
        /// </summary>
        /// <returns>The floating point value read</returns>
        public unsafe double ReadDouble()
        {
            long value = this.ReadInt64();

            return *((double*)&value);
        }

        /// <summary>
        /// Reads the specified number of bytes into the given buffer, starting at
        /// the given index.
        /// </summary>
        /// <param name="buffer">The buffer to copy data into</param>
        /// <param name="index">The first index to copy data into</param>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The number of bytes actually read. This will only be less than
        /// the requested number of bytes if the end of the stream is reached.
        /// </returns>
        public int Read(byte[] buffer, int index, int count)
        {
            this.CheckDisposed();

            Guard.NotNull(this.storageBuffer, nameof(this.storageBuffer));
            Guard.MustBeGreaterThanOrEqualTo(index, 0, nameof(index));
            Guard.MustBeGreaterThanOrEqualTo(count, 0, nameof(count));
            Guard.IsFalse(count + index > buffer.Length, nameof(buffer.Length), "Not enough space in buffer for specified number of bytes starting at specified index.");

            int read = 0;
            while (count > 0)
            {
                int block = this.BaseStream.Read(buffer, index, count);
                if (block == 0)
                {
                    return read;
                }

                index += block;
                read += block;
                count -= block;
            }

            return read;
        }

        /// <summary>
        /// Reads the specified number of bytes, returning them in a new byte array.
        /// If not enough bytes are available before the end of the stream, this
        /// method will return what is available.
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The bytes read</returns>
        public byte[] ReadBytes(int count)
        {
            this.CheckDisposed();
            Guard.MustBeGreaterThanOrEqualTo(count, 0, nameof(count));

            byte[] ret = new byte[count];
            int index = 0;
            while (index < count)
            {
                int read = this.BaseStream.Read(ret, index, count - index);

                // Stream has finished half way through. That's fine, return what we've got.
                if (read == 0)
                {
                    byte[] copy = new byte[index];
                    Buffer.BlockCopy(ret, 0, copy, 0, index);
                    return copy;
                }

                index += read;
            }

            return ret;
        }

        /// <summary>
        /// Reads the specified number of bytes, returning them in a new byte array.
        /// If not enough bytes are available before the end of the stream, this
        /// method will throw an IOException.
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The bytes read</returns>
        public byte[] ReadBytesOrThrow(int count)
        {
            byte[] ret = new byte[count];
            this.ReadInternal(ret, count);
            return ret;
        }

        /// <summary>
        /// Disposes of the underlying stream.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;
                ((IDisposable)this.BaseStream).Dispose();
            }
        }

        /// <summary>
        /// Checks whether or not the reader has been disposed, throwing an exception if so.
        /// </summary>
        private void CheckDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(EndianBinaryReader));
            }
        }

        /// <summary>
        /// Reads the given number of bytes from the stream, throwing an exception
        /// if they can't all be read.
        /// </summary>
        /// <param name="data">Buffer to read into</param>
        /// <param name="size">Number of bytes to read</param>
        private void ReadInternal(byte[] data, int size)
        {
            this.CheckDisposed();
            int index = 0;
            while (index < size)
            {
                int read = this.BaseStream.Read(data, index, size - index);
                if (read == 0)
                {
                    throw new EndOfStreamException($"End of stream reached with {size - index} byte{(size - index == 1 ? "s" : string.Empty)} left to read.");
                }

                index += read;
            }
        }

        /// <summary>
        /// Reads the given number of bytes from the stream if possible, returning
        /// the number of bytes actually read, which may be less than requested if
        /// (and only if) the end of the stream is reached.
        /// </summary>
        /// <param name="data">Buffer to read into</param>
        /// <param name="size">Number of bytes to read</param>
        /// <returns>Number of bytes actually read</returns>
        private int TryReadInternal(byte[] data, int size)
        {
            this.CheckDisposed();
            int index = 0;
            while (index < size)
            {
                int read = this.BaseStream.Read(data, index, size - index);
                if (read == 0)
                {
                    return index;
                }

                index += read;
            }

            return index;
        }
    }
}