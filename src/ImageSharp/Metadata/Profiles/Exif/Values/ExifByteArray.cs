// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifByteArray : ExifArrayValue<byte>
    {
        public ExifByteArray(ExifTag<byte[]> tag, ExifDataType dataType)
            : base(tag) => this.DataType = dataType;

        public ExifByteArray(ExifTagValue tag, ExifDataType dataType)
            : base(tag) => this.DataType = dataType;

        private ExifByteArray(ExifByteArray value)
            : base(value) => this.DataType = value.DataType;

        public override ExifDataType DataType { get; }

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            if (value is int[] intArrayValue)
            {
                return this.TrySetSignedIntArray(intArrayValue);
            }

            if (value is int intValue)
            {
                if (intValue >= byte.MinValue && intValue <= byte.MaxValue)
                {
                    this.Value = new byte[] { (byte)intValue };
                }

                return true;
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifByteArray(this);

        private bool TrySetSignedIntArray(int[] intArrayValue)
        {
            if (Array.FindIndex(intArrayValue, x => x < byte.MinValue || x > byte.MaxValue) > -1)
            {
                return false;
            }

            var value = new byte[intArrayValue.Length];
            for (int i = 0; i < intArrayValue.Length; i++)
            {
                int s = intArrayValue[i];
                value[i] = (byte)s;
            }

            this.Value = value;
            return true;
        }
    }
}
