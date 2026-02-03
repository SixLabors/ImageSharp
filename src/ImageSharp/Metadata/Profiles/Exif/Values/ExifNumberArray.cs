// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

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
            if (this.Value is not null)
            {
                foreach (Number value in this.Value)
                {
                    if (value > ushort.MaxValue)
                    {
                        return ExifDataType.Long;
                    }
                }
            }

            return ExifDataType.Short;
        }
    }

    public override bool TrySetValue(object? value)
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
            case short val:
                return this.SetSingle(val);
            case ushort val:
                return this.SetSingle(val);
            case int[] array:
                // workaround for inconsistent covariance of value-typed arrays
                if (value.GetType() == typeof(uint[]))
                {
                    return this.SetArray((uint[])value);
                }

                return this.SetArray(array);

            case short[] array:
                if (value.GetType() == typeof(ushort[]))
                {
                    return this.SetArray((ushort[])value);
                }

                return this.SetArray(array);
        }

        return false;
    }

    public override IExifValue DeepClone() => new ExifNumberArray(this);

    private bool SetSingle(Number value)
    {
        this.Value = [value];
        return true;
    }

    private bool SetArray(int[] values)
    {
        Number[] numbers = new Number[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            numbers[i] = values[i];
        }

        this.Value = numbers;
        return true;
    }

    private bool SetArray(uint[] values)
    {
        Number[] numbers = new Number[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            numbers[i] = values[i];
        }

        this.Value = numbers;
        return true;
    }

    private bool SetArray(short[] values)
    {
        Number[] numbers = new Number[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            numbers[i] = values[i];
        }

        this.Value = numbers;
        return true;
    }

    private bool SetArray(ushort[] values)
    {
        Number[] numbers = new Number[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            numbers[i] = values[i];
        }

        this.Value = numbers;
        return true;
    }
}
