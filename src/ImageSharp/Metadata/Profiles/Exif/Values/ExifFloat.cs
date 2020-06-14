// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifFloat : ExifValue<float>
    {
        public ExifFloat(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifFloat(ExifFloat value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SingleFloat;

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

        public override IExifValue DeepClone() => new ExifFloat(this);
    }
}
