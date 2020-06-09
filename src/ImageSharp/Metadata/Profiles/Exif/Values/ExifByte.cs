// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifByte : ExifValue<byte>
    {
        public ExifByte(ExifTag<byte> tag, ExifDataType dataType)
            : base(tag) => this.DataType = dataType;

        public ExifByte(ExifTagValue tag, ExifDataType dataType)
            : base(tag) => this.DataType = dataType;

        private ExifByte(ExifByte value)
            : base(value) => this.DataType = value.DataType;

        public override ExifDataType DataType { get; }

        protected override string StringValue => this.Value.ToString("X2", CultureInfo.InvariantCulture);

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            switch (value)
            {
                case int intValue:
                    if (intValue >= byte.MinValue && intValue <= byte.MaxValue)
                    {
                        this.Value = (byte)intValue;
                        return true;
                    }

                    return false;
                default:
                    return base.TrySetValue(value);
            }
        }

        public override IExifValue DeepClone() => new ExifByte(this);
    }
}
