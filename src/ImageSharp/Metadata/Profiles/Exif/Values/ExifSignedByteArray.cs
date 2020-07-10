// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedByteArray : ExifArrayValue<sbyte>
    {
        public ExifSignedByteArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedByteArray(ExifSignedByteArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedByte;

        public override IExifValue DeepClone() => new ExifSignedByteArray(this);
    }
}
