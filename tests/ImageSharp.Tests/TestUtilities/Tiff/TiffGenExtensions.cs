// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// A utility class for generating in-memory Tiff files for use in unit tests.
    /// </summary>
    internal static class TiffGenExtensions
    {
        public static byte[] ToBytes(this ITiffGenDataSource dataSource, bool isLittleEndian)
        {
            var dataBlocks = dataSource.GetData(isLittleEndian);

            int offset = 0;

            foreach (var dataBlock in dataBlocks)
            {
                byte[] offsetBytes = BitConverter.GetBytes(offset).WithByteOrder(isLittleEndian);

                foreach (var reference in dataBlock.References)
                {
                    reference.Bytes[reference.Offset + 0] = offsetBytes[0];
                    reference.Bytes[reference.Offset + 1] = offsetBytes[1];
                    reference.Bytes[reference.Offset + 2] = offsetBytes[2];
                    reference.Bytes[reference.Offset + 3] = offsetBytes[3];
                }

                offset += dataBlock.Bytes.Length;
            }

            return dataBlocks.SelectMany(b => b.Bytes).ToArray();
        }

        public static Stream ToStream(this ITiffGenDataSource dataSource, bool isLittleEndian)
        {
            var bytes = dataSource.ToBytes(isLittleEndian);
            return new MemoryStream(bytes);
        }
    }
}