// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifRationalArray : ExifArrayValue<Rational>
    {
        public ExifRationalArray(ExifTag<Rational[]> tag)
            : base(tag)
        {
        }

        public ExifRationalArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifRationalArray(ExifRationalArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Rational;

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            if (value is SignedRational[] signedArray)
            {
                return this.TrySetSignedArray(signedArray);
            }

            if (value is SignedRational signed)
            {
                if (signed.Numerator >= 0 && signed.Denominator >= 0)
                {
                    this.Value = new[] { new Rational((uint)signed.Numerator, (uint)signed.Denominator) };
                }

                return true;
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifRationalArray(this);

        private bool TrySetSignedArray(SignedRational[] signed)
        {
            if (Array.FindIndex(signed, x => x.Numerator < 0 || x.Denominator < 0) > -1)
            {
                return false;
            }

            var unsigned = new Rational[signed.Length];
            for (int i = 0; i < signed.Length; i++)
            {
                SignedRational s = signed[i];
                unsigned[i] = new Rational((uint)s.Numerator, (uint)s.Denominator);
            }

            this.Value = unsigned;
            return true;
        }
    }
}
