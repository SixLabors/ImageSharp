// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedLong8Array : ExifArrayValue<long>
    {
        public ExifSignedLong8Array(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedLong8Array(ExifSignedLong8Array value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedLong8;

        public override IExifValue DeepClone() => new ExifSignedLong8Array(this);
    }
}
