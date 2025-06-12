// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

internal sealed class ExifByteArray : ExifArrayValue<byte>
{
    public ExifByteArray(ExifTag<byte[]> tag, ExifDataType dataType)
        : base(tag) => this.DataType = dataType;

    public ExifByteArray(ExifTagValue tag, ExifDataType dataType)
        : base(tag) => this.DataType = dataType;

    private ExifByteArray(ExifByteArray value)
        : base(value) => this.DataType = value.DataType;

    public override ExifDataType DataType { get; }

    public override bool TrySetValue(object? value)
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
            if (intValue is >= byte.MinValue and <= byte.MaxValue)
            {
                this.Value = [(byte)intValue];
            }

            return true;
        }

        return false;
    }

    public override IExifValue DeepClone() => new ExifByteArray(this);

    private bool TrySetSignedIntArray(int[] intArrayValue)
    {
        if (Array.FindIndex(intArrayValue, x => (uint)x > byte.MaxValue) >= 0)
        {
            return false;
        }

        byte[] value = new byte[intArrayValue.Length];
        for (int i = 0; i < intArrayValue.Length; i++)
        {
            int s = intArrayValue[i];
            value[i] = (byte)s;
        }

        this.Value = value;
        return true;
    }
}
