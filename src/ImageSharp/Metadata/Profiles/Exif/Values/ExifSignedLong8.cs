// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedLong8 : ExifValue<long>
    {
        public ExifSignedLong8(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedLong8(ExifSignedLong8 value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedLong8;

        protected override string StringValue => this.Value.ToString(CultureInfo.InvariantCulture);

        public override IExifValue DeepClone() => new ExifSignedLong8(this);
    }
}
