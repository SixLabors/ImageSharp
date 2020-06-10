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

        public override IExifValue DeepClone() => new ExifNumberArray(this);
    }
}
