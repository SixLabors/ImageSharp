// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class ColorTrcCalculator : IVector4Calculator
{
    private readonly TrcCalculator curveCalculator;
    private Matrix4x4 matrix;
    private readonly bool toPcs;

    public ColorTrcCalculator(
        IccXyzTagDataEntry redMatrixColumn,
        IccXyzTagDataEntry greenMatrixColumn,
        IccXyzTagDataEntry blueMatrixColumn,
        IccTagDataEntry redTrc,
        IccTagDataEntry greenTrc,
        IccTagDataEntry blueTrc,
        bool toPcs)
    {
        this.toPcs = toPcs;
        this.curveCalculator = new TrcCalculator(new IccTagDataEntry[] { redTrc, greenTrc, blueTrc }, !toPcs);

        Vector3 mr = redMatrixColumn.Data[0];
        Vector3 mg = greenMatrixColumn.Data[0];
        Vector3 mb = blueMatrixColumn.Data[0];
        this.matrix = new Matrix4x4(mr.X, mr.Y, mr.Z, 0, mg.X, mg.Y, mg.Z, 0, mb.X, mb.Y, mb.Z, 0, 0, 0, 0, 1);

        if (!toPcs)
        {
            Matrix4x4.Invert(this.matrix, out this.matrix);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Calculate(Vector4 value)
    {
        // uses the descaled XYZ as DemoMaxICC IccCmm.cpp : CIccXformMatrixTRC::Apply()
        Vector4 xyz = new(CieXyz.FromScaledVector4(value).ToVector3(), 1);

        if (this.toPcs)
        {
            value = this.curveCalculator.Calculate(xyz);
            return Vector4.Transform(value, this.matrix);
        }

        value = Vector4.Transform(xyz, this.matrix);
        return this.curveCalculator.Calculate(value);
    }
}
