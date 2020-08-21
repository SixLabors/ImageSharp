// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifDouble : ExifValue<double>
    {
        public ExifDouble(ExifTag<double> tag)
            : base(tag)
        {
        }

        public ExifDouble(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifDouble(ExifDouble value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.DoubleFloat;

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
                    this.Value = intValue;
                    return true;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifDouble(this);
    }
}
