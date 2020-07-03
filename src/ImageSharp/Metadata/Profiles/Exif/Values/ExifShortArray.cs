// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifShortArray : ExifArrayValue<ushort>
    {
        public ExifShortArray(ExifTag<ushort[]> tag)
            : base(tag)
        {
        }

        public ExifShortArray(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifShortArray(ExifShortArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Short;

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            if (value is int[] signedIntArray)
            {
                return this.TrySetSignedIntArray(signedIntArray);
            }

            if (value is short[] signedShortArray)
            {
                return this.TrySetSignedShortArray(signedShortArray);
            }

            if (value is int signedInt)
            {
                if (signedInt >= ushort.MinValue && signedInt <= ushort.MaxValue)
                {
                    this.Value = new ushort[] { (ushort)signedInt };
                }

                return true;
            }

            if (value is short signedShort)
            {
                if (signedShort >= ushort.MinValue)
                {
                    this.Value = new ushort[] { (ushort)signedShort };
                }

                return true;
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifShortArray(this);

        private bool TrySetSignedIntArray(int[] signed)
        {
            if (Array.FindIndex(signed, x => x < ushort.MinValue || x > ushort.MaxValue) > -1)
            {
                return false;
            }

            var unsigned = new ushort[signed.Length];
            for (int i = 0; i < signed.Length; i++)
            {
                int s = signed[i];
                unsigned[i] = (ushort)s;
            }

            this.Value = unsigned;
            return true;
        }

        private bool TrySetSignedShortArray(short[] signed)
        {
            if (Array.FindIndex(signed, x => x < ushort.MinValue) > -1)
            {
                return false;
            }

            var unsigned = new ushort[signed.Length];
            for (int i = 0; i < signed.Length; i++)
            {
                short s = signed[i];
                unsigned[i] = (ushort)s;
            }

            this.Value = unsigned;
            return true;
        }
    }
}
