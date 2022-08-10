// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedLongArray : ExifArrayValue<int>
    {
        public ExifSignedLongArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedLongArray(ExifSignedLongArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedLong;

        public override IExifValue DeepClone() => new ExifSignedLongArray(this);
    }
}
