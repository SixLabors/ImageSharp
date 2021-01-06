// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;

using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// The TIFF IFD reader class.
    /// </summary>
    internal class DirectoryReader
    {
        private readonly ByteOrder byteOrder;

        private readonly Stream stream;

        private uint nextIfdOffset;

        public DirectoryReader(ByteOrder byteOrder, Stream stream)
        {
            this.byteOrder = byteOrder;
            this.stream = stream;
        }

        public IEnumerable<ExifProfile> Read()
        {
            this.nextIfdOffset = new HeaderReader(this.byteOrder, this.stream).ReadFileHeader();

            IEnumerable<List<IExifValue>> ifdList = this.ReadIfds();

            var list = new List<ExifProfile>();
            foreach (List<IExifValue> ifd in ifdList)
            {
                var profile = new ExifProfile();
                profile.InitializeInternal(ifd);
                list.Add(profile);
            }

            return list;
        }

        private IEnumerable<List<IExifValue>> ReadIfds()
        {
            var valuesList = new List<List<IExifValue>>();
            var readersList = new SortedList<uint, EntryReader>();
            while (this.nextIfdOffset != 0)
            {
                var reader = new EntryReader(this.byteOrder, this.stream, this.nextIfdOffset);
                List<IExifValue> values = reader.ReadValues();
                valuesList.Add(values);

                this.nextIfdOffset = reader.NextIfdOffset;

                if (reader.BigValuesOffset.HasValue)
                {
                    readersList.Add(reader.BigValuesOffset.Value, reader);
                }
            }

            // sequential reading big values
            foreach (EntryReader reader in readersList.Values)
            {
                reader.LoadBigValues();
            }

            return valuesList;
        }
    }
}
