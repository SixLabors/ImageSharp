// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal abstract class ExifArrayValue<TValueType> : ExifValue, IExifValue<TValueType[]>
    {
        protected ExifArrayValue(ExifTag<TValueType[]> tag)
            : base(tag)
        {
        }

        protected ExifArrayValue(ExifTagValue tag)
            : base(tag)
        {
        }

        internal ExifArrayValue(ExifArrayValue<TValueType> value)
            : base(value)
        {
        }

        public override bool IsArray => true;

        public TValueType[] Value { get; set; }

        public override object GetValue() => this.Value;

        public override bool TrySetValue(object value)
        {
            if (value is null)
            {
                this.Value = null;
                return true;
            }

            Type type = value.GetType();
            if (value.GetType() == typeof(TValueType[]))
            {
                this.Value = (TValueType[])value;
                return true;
            }

            if (type == typeof(TValueType))
            {
                this.Value = new TValueType[] { (TValueType)value };
                return true;
            }

            return false;
        }
    }
}
