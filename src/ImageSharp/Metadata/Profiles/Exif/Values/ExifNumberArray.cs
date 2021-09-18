// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
                    return this.SetSingle(val);
                case uint val:
                    return this.SetSingle(val);
                case ushort val:
                    return this.SetSingle(val);
                case short val:
                    return this.SetSingle(val);
                case uint[] array:
                    return this.SetArray(array);
                case int[] array:
                    return this.SetArray(array);
                case ushort[] array:
                    return this.SetArray(array);
                case short[] array:
                    return this.SetArray(array);
            }

            return false;
        }

        public override IExifValue DeepClone() => new ExifNumberArray(this);

        private bool SetSingle(Number value)
        {
            this.Value = new[] { value };
            return true;
        }

        private bool SetArray(int[] values)
        {
            var numbers = new Number[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                numbers[i] = values[i];
            }

            this.Value = numbers;
            return true;
        }

        private bool SetArray(uint[] values)
        {
            var numbers = new Number[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                numbers[i] = values[i];
            }

            this.Value = numbers;
            return true;
        }

        private bool SetArray(short[] values)
        {
            var numbers = new Number[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                numbers[i] = values[i];
            }

            this.Value = numbers;
            return true;
        }

        private bool SetArray(ushort[] values)
        {
            var numbers = new Number[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                numbers[i] = values[i];
            }

            this.Value = numbers;
            return true;
        }
    }
}
