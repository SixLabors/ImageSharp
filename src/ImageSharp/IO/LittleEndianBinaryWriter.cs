// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A Little Endian variant of <see cref="BinaryWriter"/>
    /// </summary>
    internal sealed class LittleEndianBinaryWriter : EndianBinaryWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LittleEndianBinaryWriter"/> class
        /// </summary>
        /// <param name="stream">Stream to write data to</param>
        public LittleEndianBinaryWriter(Stream stream)
            : base(stream)
        {
        }

        /// <inhertdoc />
        public override Endianness Endianness => Endianness.LittleEndian;

        /// <inheritdoc />
        public override void Write(short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(this.buffer, value);

            this.WriteInternal(this.buffer, 2);
        }

        /// <inheritdoc />
        public override void Write(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(this.buffer, value);

            this.WriteInternal(this.buffer, 4);
        }

        /// <inheritdoc />
        public override void Write(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(this.buffer, value);

            this.WriteInternal(this.buffer, 8);
        }

        /// <inheritdoc />
        public override void Write(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(this.buffer, value);

            this.WriteInternal(this.buffer, 2);
        }

        /// <inheritdoc />
        public override void Write(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer, value);

            this.WriteInternal(this.buffer, 4);
        }

        /// <inheritdoc />
        public override void Write(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(this.buffer, value);

            this.WriteInternal(this.buffer, 8);
        }

        /// <inheritdoc />
        public override unsafe void Write(float value)
        {
            this.Write(*((int*)&value));
        }

        /// <inheritdoc />
        public override unsafe void Write(double value)
        {
            this.Write(*((long*)&value));
        }
    }
}