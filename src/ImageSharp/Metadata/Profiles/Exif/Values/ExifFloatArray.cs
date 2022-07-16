// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifFloatArray : ExifArrayValue<float>
    {
        public ExifFloatArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifFloatArray(ExifFloatArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SingleFloat;

        public override IExifValue DeepClone() => new ExifFloatArray(this);
    }
}
