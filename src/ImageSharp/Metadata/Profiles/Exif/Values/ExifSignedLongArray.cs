// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
