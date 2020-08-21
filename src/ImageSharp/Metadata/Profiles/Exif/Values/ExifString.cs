// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifString : ExifValue<string>
    {
        public ExifString(ExifTag<string> tag)
            : base(tag)
        {
        }

        public ExifString(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifString(ExifString value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Ascii;

        protected override string StringValue => this.Value;

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            switch (value)
            {
                case int intValue:
                    this.Value = intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifString(this);
    }
}
