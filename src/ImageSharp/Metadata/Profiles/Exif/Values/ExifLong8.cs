// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class ExifLong8 : ExifValue<ulong>
    {
        public ExifLong8(ExifTag<ulong> tag)
            : base(tag)
        {
        }

        public ExifLong8(ExifTagValue tag)
            : base(tag)
        {
        }

        private ExifLong8(ExifLong8 value)
            : base(value)
        {
        }

        public override ExifDataType DataType => ExifDataType.Long8;

        protected override string StringValue => this.Value.ToString(CultureInfo.InvariantCulture);

        public override bool TrySetValue(object value)
        {
            if (base.TrySetValue(value))
            {
                return true;
            }

            switch (value)
            {
                case int intValue:
                    if (intValue >= uint.MinValue)
                    {
                        this.Value = (uint)intValue;
                        return true;
                    }

                    return false;
                case uint uintValue:
                    this.Value = uintValue;

                    return true;
                case long intValue:
                    if (intValue >= 0)
                    {
                        this.Value = (ulong)intValue;
                        return true;
                    }

                    return false;
                default:

                    return false;
            }
        }

        public override IExifValue DeepClone() => new ExifLong8(this);
    }
}
