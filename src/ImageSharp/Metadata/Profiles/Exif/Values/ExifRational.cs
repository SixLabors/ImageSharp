// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifRational : ExifValue<Rational>
    {
        public ExifRational(ExifTag<Rational> tag)
            : base(tag)
        {
        }

        public ExifRational(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifRational(ExifRational value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Rational;

        protected override string StringValue => this.Value.ToString(CultureInfo.InvariantCulture);

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            switch (value)
            {
                case SignedRational signed:

                    if (signed.Numerator >= uint.MinValue && signed.Denominator >= uint.MinValue)
                    {
                        this.Value = new Rational((uint)signed.Numerator, (uint)signed.Denominator);
                    }

                    return true;
                default:
                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifRational(this);
    }
}
