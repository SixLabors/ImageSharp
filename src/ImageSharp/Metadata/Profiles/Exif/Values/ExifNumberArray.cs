// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifNumberArray : ExifArrayValue<Number>
    {
        public ExifNumberArray(ExifTag<Number[]> tag)
            : base(tag)
        {
        }

        private ExifNumberArray(ExifNumberArray value)
            : base(value)
        {
        }

        public override ExifDataType DataType
        {
            get
            {
                if (this.Value is null)
                {
                    return ExifDataType.Short;
                }

                for (int i = 0; i < this.Value.Length; i++)
                {
                    if (this.Value[i] > ushort.MaxValue)
                    {
                        return ExifDataType.Long;
                    }
                }

                return ExifDataType.Short;
            }
        }

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            switch (value)
            {
                case int val:
                    return this.SetSingle(val, v => (Number)v);
                case uint val:
                    return this.SetSingle(val, v => (Number)v);
                case short val:
                    return this.SetSingle(val, v => (Number)v);
                case ushort val:
                    return this.SetSingle(val, v => (Number)v);
                case int[] array:
                    return this.SetArray(array, v => (Number)v);
                case uint[] array:
                    return this.SetArray(array, v => (Number)v);
                case short[] array:
                    return this.SetArray(array, v => (Number)v);
                case ushort[] array:
                    return this.SetArray(array, v => (Number)v);
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifNumberArray(this);

        private bool SetSingle<T>(T value, Func<T, Number> converter)
        {
            this.Value = new Number[] { converter(value) };
            return true;
        }

        private bool SetArray<T>(T[] values, Func<T, Number> converter)
        {
            var numbers = new Number[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                numbers[i] = converter(values[i]);
            }

            this.Value = numbers;
            return true;
        }
    }
}
