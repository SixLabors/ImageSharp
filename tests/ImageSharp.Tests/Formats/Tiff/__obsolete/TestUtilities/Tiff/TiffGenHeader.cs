// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A utility data structure to represent a Tiff file-header.
    /// </summary>
    internal class TiffGenHeader : ITiffGenDataSource
    {
        public TiffGenHeader()
        {
            this.MagicNumber = 42;
        }

        public ushort? ByteOrderMarker { get; set; }
        public ushort MagicNumber { get; set; }
        public TiffGenIfd FirstIfd { get; set; }

        public IEnumerable<TiffGenDataBlock> GetData(bool isLittleEndian)
        {
            ByteBuffer bytes = new ByteBuffer(isLittleEndian);

            bytes.AddUInt16(ByteOrderMarker ?? (isLittleEndian ? (ushort)0x4949 : (ushort)0x4D4D));
            bytes.AddUInt16(MagicNumber);
            bytes.AddUInt32(0);

            var headerData = new TiffGenDataBlock(bytes.ToArray());

            if (FirstIfd != null)
            {
                var firstIfdData = FirstIfd.GetData(isLittleEndian);
                firstIfdData.First().AddReference(headerData.Bytes, 4);
                return new[] { headerData }.Concat(firstIfdData);
            }
            else
            {
                return new[] { headerData };
            }
        }
    }
}