// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class LutCalculator : ISingleCalculator
{
    private readonly float[] lut;
    private readonly bool inverse;

    public LutCalculator(float[] lut, bool inverse)
    {
        Guard.NotNull(lut, nameof(lut));

        this.lut = lut;
        this.inverse = inverse;
    }

    public float Calculate(float value)
    {
        if (this.inverse)
        {
            return this.LookupInverse(value);
        }

        return this.Lookup(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Lookup(float value)
    {
        value = Math.Max(value, 0);

        float factor = value * (this.lut.Length - 1);
        int index = (int)factor;
        float low = this.lut[index];

        float high = 1F;
        if (index < this.lut.Length - 1)
        {
            high = this.lut[index + 1];
        }

        return low + ((high - low) * (factor - index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float LookupInverse(float value)
    {
        int index = Array.BinarySearch(this.lut, value);
        if (index >= 0)
        {
            return index / (float)(this.lut.Length - 1);
        }

        index = ~index;
        if (index == 0)
        {
            return 0;
        }
        else if (index == this.lut.Length)
        {
            return 1;
        }

        float high = this.lut[index];
        float low = this.lut[index - 1];

        float valuePercent = (value - low) / (high - low);
        float lutRange = 1 / (float)(this.lut.Length - 1);
        float lutLow = (index - 1) / (float)(this.lut.Length - 1);

        return lutLow + (valuePercent * lutRange);
    }
}
