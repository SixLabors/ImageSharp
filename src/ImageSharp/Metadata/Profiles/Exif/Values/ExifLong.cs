// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifLong : ExifValue<uint>
    {
        public ExifLong(ExifTag<uint> tag)
            : base(tag)
        {
        }

        public ExifLong(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifLong(ExifLong value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Long;

        protected override string StringValue => this.Value.ToString(CultureInfo.InvariantCulture);

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            switch (value)
            {
                case int intValue:
                    if (intValue >= uint.MinValue)
                    {
                        this.Value = (uint)intValue;
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifLong(this);
    }
}
