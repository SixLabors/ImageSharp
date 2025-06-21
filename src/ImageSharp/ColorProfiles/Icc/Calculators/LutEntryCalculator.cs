// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class LutEntryCalculator : IVector4Calculator
{
    private LutCalculator[] inputCurve;
    private LutCalculator[] outputCurve;
    private ClutCalculator clutCalculator;
    private Matrix4x4 matrix;
    private bool doTransform;

    public LutEntryCalculator(IccLut8TagDataEntry lut)
    {
        Guard.NotNull(lut, nameof(lut));
        this.Init(lut.InputValues, lut.OutputValues, lut.ClutValues, lut.Matrix);
        this.Is16Bit = false;
    }

    public LutEntryCalculator(IccLut16TagDataEntry lut)
    {
        Guard.NotNull(lut, nameof(lut));
        this.Init(lut.InputValues, lut.OutputValues, lut.ClutValues, lut.Matrix);
        this.Is16Bit = true;
    }

    internal bool Is16Bit { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Calculate(Vector4 value)
    {
        if (this.doTransform)
        {
            value = Vector4.Transform(value, this.matrix);
        }

        value = CalculateLut(this.inputCurve, value);
        value = this.clutCalculator.Calculate(value);
        return CalculateLut(this.outputCurve, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 CalculateLut(LutCalculator[] lut, Vector4 value)
    {
        ref float f = ref Unsafe.As<Vector4, float>(ref value);
        for (int i = 0; i < lut.Length; i++)
        {
            Unsafe.Add(ref f, i) = lut[i].Calculate(Unsafe.Add(ref f, i));
        }

        return value;
    }

    private void Init(IccLut[] inputCurve, IccLut[] outputCurve, IccClut clut, Matrix4x4 matrix)
    {
        this.inputCurve = InitLut(inputCurve);
        this.outputCurve = InitLut(outputCurve);
        this.clutCalculator = new(clut);
        this.matrix = matrix;

        this.doTransform = !matrix.IsIdentity && inputCurve.Length == 3;
    }

    private static LutCalculator[] InitLut(IccLut[] curves)
    {
        LutCalculator[] calculators = new LutCalculator[curves.Length];
        for (int i = 0; i < curves.Length; i++)
        {
            calculators[i] = new(curves[i].Values, false);
        }

        return calculators;
    }
}
