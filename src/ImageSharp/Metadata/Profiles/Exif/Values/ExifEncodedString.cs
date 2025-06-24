// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

internal sealed class ExifEncodedString : ExifValue<EncodedString>
{
    public ExifEncodedString(ExifTag<EncodedString> tag)
        : base(tag)
    {
    }

    public ExifEncodedString(ExifTagValue tag)
        : base(tag)
    {
    }

    private ExifEncodedString(ExifEncodedString value)
        : base(value)
    {
    }

    public override ExifDataType DataType => ExifDataType.Undefined;

    protected override string StringValue => this.Value.Text;

    public bool TrySetValue(object? value, ByteOrder order)
    {
        if (base.TrySetValue(value))
        {
            return true;
        }

        if (value is string stringValue)
        {
            this.Value = new(stringValue);
            return true;
        }
        else if (value is byte[] buffer)
        {
            if (ExifEncodedStringHelpers.TryParse(buffer, order, out EncodedString encodedString))
            {
                this.Value = encodedString;
                return true;
            }
        }

        return false;
    }

    public override bool TrySetValue(object? value)
        => this.TrySetValue(value, ByteOrder.LittleEndian);

    public override IExifValue DeepClone() => new ExifEncodedString(this);
}
