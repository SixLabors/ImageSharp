// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifLong8Array : ExifArrayValue<ulong>
    {
        public ExifLong8Array(ExifTag<ulong[]> tag)
            : base(tag)
        {
        }

        public ExifLong8Array(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifLong8Array(ExifLong8Array value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Long8;

        public override IExifValue DeepClone() => new ExifLong8Array(this);
    }
}
