// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.IO;
using Xunit;

namespace SixLabors.ImageSharp.Tests.IO
{
    public class EndianBinaryReaderWriterTests
    {
        [Fact]
        public void RoundtripSingles()
        {
            foreach ((Endianness endianness, byte[] bytes) in new[] {
                (Endianness.BigEndian, new byte[] { 64, 73, 15, 219 }),
                (Endianness.LittleEndian, new byte[] { 219, 15, 73, 64 })
            })
            {
                var stream = new MemoryStream();

                using (var writer = EndianBinaryWriter.Create(endianness, stream))
                {
                    writer.Write((float)Math.PI);

                    Assert.Equal(bytes, stream.ToArray());
                }
            }
        }

        [Fact]
        public void RoundtripDoubles()
        {
            foreach ((Endianness endianness, byte[] bytes) in new[] {
                (Endianness.BigEndian, new byte[] { 64, 9, 33, 251, 84, 68, 45, 24 }),
                (Endianness.LittleEndian, new byte[] { 24, 45, 68, 84, 251, 33, 9, 64 })
            })
            {
                var stream = new MemoryStream();

                using (var writer = EndianBinaryWriter.Create(endianness, stream))
                {
                    writer.Write(Math.PI);

                    Assert.Equal(bytes, stream.ToArray());
                }
            }
        }

        /// <summary>
        /// Ensures that the data written through a binary writer can be read back through the reader
        /// </summary>
        [Fact]
        public void RoundtripValues()
        {
            foreach (Endianness endianness in new[] { Endianness.BigEndian, Endianness.LittleEndian })
            {
                var stream = new MemoryStream();

                var writer = EndianBinaryWriter.Create(endianness, stream);

                writer.Write(true);         // Bool
                writer.Write((byte)1);      // Byte
                writer.Write((short)1);     // Int16
                writer.Write(1);            // Int32
                writer.Write(1L);           // Int64
                writer.Write(1f);           // Single
                writer.Write(1d);           // Double
                writer.Write((sbyte)1);     // SByte
                writer.Write((ushort)1);    // UInt16
                writer.Write((uint)1);      // UInt32
                writer.Write(1UL);          // ULong

                Assert.Equal(43, stream.Length);

                stream.Position = 0;

                var reader = new EndianBinaryReader(endianness, stream);

                Assert.True(reader.ReadBoolean());             // Bool
                Assert.Equal((byte)1, reader.ReadByte());      // Byte
                Assert.Equal((short)1, reader.ReadInt16());    // Int16
                Assert.Equal(1, reader.ReadInt32());           // Int32
                Assert.Equal(1L, reader.ReadInt64());          // Int64
                Assert.Equal(1f, reader.ReadSingle());         // Single
                Assert.Equal(1d, reader.ReadDouble());         // Double
                Assert.Equal((sbyte)1, reader.ReadSByte());    // SByte
                Assert.Equal((ushort)1, reader.ReadUInt16());  // UInt16
                Assert.Equal((uint)1, reader.ReadUInt32());    // UInt32
                Assert.Equal(1UL, reader.ReadUInt64());        // ULong

                stream.Dispose();
            }
        }
    }
}