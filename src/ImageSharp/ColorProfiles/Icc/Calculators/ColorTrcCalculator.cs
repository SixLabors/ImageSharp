// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class ColorTrcCalculator : IVector4Calculator
{
    private readonly TrcCalculator curveCalculator;
    private readonly Matrix4x4 matrix;
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
        this.curveCalculator = new TrcCalculator([redTrc, greenTrc, blueTrc], !toPcs);

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
        if (this.toPcs)
        {
            // input is always linear RGB
            value = this.curveCalculator.Calculate(value);
            CieXyz xyz = new(Vector4.Transform(value, this.matrix).AsVector3());

            // when data to PCS, output from calculator is descaled XYZ
            // but downstream process requires scaled XYZ
            // (see DemoMaxICC IccCmm.cpp : CIccXformMatrixTRC::Apply)
            return xyz.ToScaledVector4();
        }
        else
        {
            // input is always XYZ
            Vector4 xyz = Vector4.Transform(value, this.matrix);

            // when data to PCS, upstream process provides scaled XYZ
            // but input to calculator is descaled XYZ
            // (see DemoMaxICC IccCmm.cpp : CIccXformMatrixTRC::Apply)
            xyz = new Vector4(CieXyz.FromScaledVector4(xyz).AsVector3Unsafe(), 1);
            return this.curveCalculator.Calculate(xyz);
        }
    }
}
