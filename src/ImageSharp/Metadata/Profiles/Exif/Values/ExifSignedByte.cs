// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

internal sealed class ExifSignedByte : ExifValue<sbyte>
{
    public ExifSignedByte(ExifTagValue tag)
        : base(tag)
    {
    }

    private ExifSignedByte(ExifSignedByte value)
        : base(value)
    {
    }

    public override ExifDataType DataType => ExifDataType.SignedByte;

    protected override string StringValue => this.Value.ToString("X2", CultureInfo.InvariantCulture);

    public override bool TrySetValue(object? value)
    {
        if (base.TrySetValue(value))
        {
            return true;
        }

        switch (value)
        {
            case int intValue:
                if (intValue is >= sbyte.MinValue and <= sbyte.MaxValue)
                {
                    this.Value = (sbyte)intValue;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    public override IExifValue DeepClone() => new ExifSignedByte(this);
}
