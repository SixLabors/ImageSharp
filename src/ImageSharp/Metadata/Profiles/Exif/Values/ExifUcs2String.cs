// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifUcs2String : ExifValue<string>
    {
        public ExifUcs2String(ExifTag<string> tag)
            : base(tag)
        {
        }

        public ExifUcs2String(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifUcs2String(ExifUcs2String value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Byte;

        protected override string StringValue => this.Value;

        public override object GetValue() => this.Value;

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            if (value is byte[] buffer)
            {
                this.Value = ExifUcs2StringHelpers.Ucs2Encoding.GetString(buffer);
                return true;
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifUcs2String(this);
    }
}
