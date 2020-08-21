// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal abstract class ExifValue<TValueType> : ExifValue, IExifValue<TValueType>
    {
        protected ExifValue(ExifTag<TValueType> tag)
            : base(tag)
        {
        }

        protected ExifValue(ExifTagValue tag)
            : base(tag)
        {
        }

        internal ExifValue(ExifValue value)
            : base(value)
        {
        }

        public TValueType Value { get; set; }

        /// <summary>
        /// Gets the value of the current instance as a string.
        /// </summary>
        protected abstract string StringValue { get; }

        public override object GetValue() => this.Value;

        public override bool TrySetValue(object value)
        {
            if (value is null)
            {
                this.Value = default;
                return true;
            }

            // We use type comparison here over "is" to avoid compiler optimizations
            // that equate short with ushort, and sbyte with byte.
            if (value.GetType() == typeof(TValueType))
            {
                this.Value = (TValueType)value;
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            if (this.Value == null)
            {
                return null;
            }

            string description = ExifTagDescriptionAttribute.GetDescription(this.Tag, this.Value);
            return description ?? this.StringValue;
        }
    }
}
