// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifNumber : ExifValue<Number>
    {
        public ExifNumber(ExifTag<Number> tag)
            : base(tag)
        {
        }

        private ExifNumber(ExifNumber value)
            : base(value)
        {
        }

        public override ExifDataType DataType
        {
            get
            {
                if (this.Value > ushort.MaxValue)
                {
                    return ExifDataType.Long;
                }

                return ExifDataType.Short;
            }
        }

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
                case uint uintValue:
                    this.Value = uintValue;
                    return true;
                case short shortValue:
                    if (shortValue >= uint.MinValue)
                    {
                        this.Value = (uint)shortValue;
                        return true;
                    }

                    return false;
                case ushort ushortValue:
                    this.Value = ushortValue;
                    return true;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifNumber(this);
    }
}
