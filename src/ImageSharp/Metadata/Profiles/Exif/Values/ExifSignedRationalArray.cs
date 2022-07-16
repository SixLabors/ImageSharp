// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedRationalArray : ExifArrayValue<SignedRational>
    {
        public ExifSignedRationalArray(ExifTag<SignedRational[]> tag)
            : base(tag)
        {
        }

        public ExifSignedRationalArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedRationalArray(ExifSignedRationalArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedRational;

        public override IExifValue DeepClone() => new ExifSignedRationalArray(this);
    }
}
