// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifShort : ExifValue<ushort>
    {
        public ExifShort(ExifTag<ushort> tag)
            : base(tag)
        {
        }

        public ExifShort(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifShort(ExifShort value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Short;

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
                    if (intValue >= ushort.MinValue && intValue <= ushort.MaxValue)
                    {
                        this.Value = (ushort)intValue;
                        return true;
                    }

                    return false;
                case short shortValue:
                    if (shortValue >= ushort.MinValue)
                    {
                        this.Value = (ushort)shortValue;
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifShort(this);
    }
}
