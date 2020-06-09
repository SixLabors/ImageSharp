// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedShort : ExifValue<short>
    {
        public ExifSignedShort(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedShort(ExifSignedShort value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedShort;

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
                    if (intValue >= short.MinValue && intValue <= short.MaxValue)
                    {
                        this.Value = (short)intValue;
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifSignedShort(this);
    }
}
