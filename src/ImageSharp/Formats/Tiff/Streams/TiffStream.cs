// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The tiff data stream base class.
    /// </summary>
    internal abstract class TiffStream
    {
        /// <summary>
        /// The input stream.
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffStream"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        protected TiffStream(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        /// Gets a value indicating whether the file is encoded in little-endian or big-endian format.
        /// </summary>
        public abstract TiffByteOrder ByteOrder { get; }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public Stream InputStream => this.stream;

        /// <summary>
        /// Gets the stream position.
        /// </summary>
        public long Position => this.stream.Position;

        public void Seek(uint offset)
        {
            this.stream.Seek(offset, SeekOrigin.Begin);
        }

        public void Skip(uint offset)
        {
            this.stream.Seek(offset, SeekOrigin.Current);
        }

        public void Skip(int offset)
        {
            this.stream.Seek(offset, SeekOrigin.Current);
        }

        /// <summary>
        /// Converts buffer data into a <see cref="byte"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public byte ReadByte()
        {
            return (byte)this.stream.ReadByte();
        }

        /// <summary>
        /// Converts buffer data into an <see cref="sbyte"/> using the correct endianness.
        /// </summary>
        /// <returns>The converted value.</returns>
        public sbyte ReadSByte()
        {
            return (sbyte)this.stream.ReadByte();
        }

        public byte[] ReadBytes(uint count)
        {
            byte[] buf = new byte[count];
            this.stream.Read(buf, 0, buf.Length);
            return buf;
        }

        public abstract short ReadInt16();

        public abstract int ReadInt32();

        public abstract uint ReadUInt32();

        public abstract ushort ReadUInt16();

        public abstract float ReadSingle();

        public abstract double ReadDouble();
    }
}
