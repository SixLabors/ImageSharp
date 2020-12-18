// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Streams;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// The TIFF IFD reader class.
    /// </summary>
    internal class DirectoryReader
    {
        private readonly TiffStream stream;

        private readonly EntryReader tagReader;

        private uint nextIfdOffset;

        public DirectoryReader(TiffStream stream)
        {
            this.stream = stream;
            this.tagReader = new EntryReader(stream);
        }

        public IEnumerable<ExifProfile> Read()
        {
            if (this.ReadHeader())
            {
                return this.ReadIfds();
            }

            return null;
        }

        private bool ReadHeader()
        {
            ushort magic = this.stream.ReadUInt16();
            if (magic != TiffConstants.HeaderMagicNumber)
            {
                TiffThrowHelper.ThrowInvalidHeader();
            }

            uint firstIfdOffset = this.stream.ReadUInt32();
            if (firstIfdOffset == 0)
            {
                TiffThrowHelper.ThrowInvalidHeader();
            }

            this.nextIfdOffset = firstIfdOffset;

            return true;
        }

        private IEnumerable<ExifProfile> ReadIfds()
        {
            var list = new List<ExifProfile>();
            while (this.nextIfdOffset != 0)
            {
                this.stream.Seek(this.nextIfdOffset);
                ExifProfile ifd = this.ReadIfd();
                list.Add(ifd);
            }

            this.tagReader.LoadExtendedData();

            return list;
        }

        private ExifProfile ReadIfd()
        {
            long pos = this.stream.Position;

            ushort entryCount = this.stream.ReadUInt16();
            var entries = new List<IExifValue>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                IExifValue tag = this.tagReader.ReadNext();
                if (tag != null)
                {
                    entries.Add(tag);
                }
            }

            this.nextIfdOffset = this.stream.ReadUInt32();

            int ifdSize = 2 + (entryCount * TiffConstants.SizeOfIfdEntry) + 4;
            int readedBytes = (int)(this.stream.Position - pos);
            int leftBytes = ifdSize - readedBytes;
            if (leftBytes > 0)
            {
                this.stream.Skip(leftBytes);
            }
            else if (leftBytes < 0)
            {
                TiffThrowHelper.ThrowOutOfRange("IFD");
            }

            var profile = new ExifProfile();
            profile.InitializeInternal(entries);
            return profile;
        }
    }
}
