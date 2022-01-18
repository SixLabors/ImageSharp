// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifEncodedString : ExifValue<EncodedString>
    {
        public ExifEncodedString(ExifTag<EncodedString> tag)
            : base(tag)
        {
        }

        public ExifEncodedString(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifEncodedString(ExifEncodedString value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Undefined;

        protected override string StringValue => this.Value.Text;

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            if (value is string stringValue)
            {
                this.Value = new EncodedString(stringValue);
                return true;
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifEncodedString(this);
    }
}
