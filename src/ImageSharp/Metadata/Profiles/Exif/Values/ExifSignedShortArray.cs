// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifSignedShortArray : ExifArrayValue<short>
    {
        public ExifSignedShortArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifSignedShortArray(ExifSignedShortArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.SignedShort;

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            if (value is int[] intArray)
            {
                return this.TrySetSignedArray(intArray);
            }

            if (value is int intValue)
            {
                if (intValue >= short.MinValue && intValue <= short.MaxValue)
                {
                    this.Value = new short[] { (short)intValue };
                }

                return true;
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifSignedShortArray(this);

        private bool TrySetSignedArray(int[] intArray)
        {
            if (Array.FindIndex(intArray, x => x < short.MinValue || x > short.MaxValue) > -1)
            {
                return false;
            }

            var value = new short[intArray.Length];
            for (int i = 0; i < intArray.Length; i++)
            {
                int s = intArray[i];
                value[i] = (short)s;
            }

            this.Value = value;
            return true;
        }
    }
}
