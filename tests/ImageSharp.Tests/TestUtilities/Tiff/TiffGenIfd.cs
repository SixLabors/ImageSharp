// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A utility data structure to represent Tiff IFDs in unit tests.
    /// </summary>
    internal class TiffGenIfd : ITiffGenDataSource
    {
        public TiffGenIfd()
        {
            this.Entries = new List<TiffGenEntry>();
        }

        public List<TiffGenEntry> Entries { get; }
        public TiffGenIfd NextIfd { get; set; }

        public IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
        {
            ByteBuffer bytes = new ByteBuffer(isLittleEndian);
            List<TiffGenDataBlock> dataBlocks = new List<TiffGenDataBlock>();
            List<Tuple<TiffGenDataBlock, int>> entryReferences = new List<Tuple<TiffGenDataBlock, int>>();

            // Add the entry count

            bytes.AddUInt16((ushort)Entries.Count);

            // Add all IFD entries

            int entryOffset = 2;

            foreach (var entry in Entries)
            {
                var entryData = entry.GetData(isLittleEndian);
                var entryBytes = entryData.First().Bytes;

                bytes.AddUInt16(entry.Tag);
                bytes.AddUInt16((ushort)entry.Type);
                bytes.AddUInt32(entry.Count);

                if (entryBytes.Length <=4)
                {
                    bytes.AddByte(entryBytes.Length > 0 ? entryBytes[0] : (byte)0);
                    bytes.AddByte(entryBytes.Length > 1 ? entryBytes[1] : (byte)0);
                    bytes.AddByte(entryBytes.Length > 2 ? entryBytes[2] : (byte)0);
                    bytes.AddByte(entryBytes.Length > 3 ? entryBytes[3] : (byte)0);

                    dataBlocks.AddRange(entryData.Skip(1));
                }
                else
                {
                    bytes.AddUInt32(0);
                    dataBlocks.AddRange(entryData);
                    entryReferences.Add(Tuple.Create(entryData.First(), entryOffset + 8));
                }
                
                entryOffset += 12;
            }

            // Add reference to next IFD
            
            bytes.AddUInt32(0);

            // Build the data

            var ifdData = new TiffGenDataBlock(bytes.ToArray());

            foreach (var entryReference in entryReferences)
            {
                entryReference.Item1.AddReference(ifdData.Bytes, entryReference.Item2);
            }

            IEnumerable<TiffGenDataBlock> nextIfdData = new TiffGenDataBlock[0];
            if (NextIfd != null)
            {
                nextIfdData = NextIfd.GetData(isLittleEndian);
                nextIfdData.First().AddReference(ifdData.Bytes, ifdData.Bytes.Length - 4); 
            }

            return new [] { ifdData }.Concat(dataBlocks).Concat(nextIfdData);
        }
    }
}