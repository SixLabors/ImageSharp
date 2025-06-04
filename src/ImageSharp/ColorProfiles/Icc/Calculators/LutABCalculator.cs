// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

internal partial class LutABCalculator : IVector4Calculator
{
    private CalculationType type;
    private TrcCalculator curveACalculator;
    private TrcCalculator curveBCalculator;
    private TrcCalculator curveMCalculator;
    private MatrixCalculator matrixCalculator;
    private ClutCalculator clutCalculator;

    public LutABCalculator(IccLutAToBTagDataEntry entry)
    {
        Guard.NotNull(entry, nameof(entry));
        this.Init(entry.CurveA, entry.CurveB, entry.CurveM, entry.Matrix3x1, entry.Matrix3x3, entry.ClutValues);
        this.type |= CalculationType.AtoB;
    }

    public LutABCalculator(IccLutBToATagDataEntry entry)
    {
        Guard.NotNull(entry, nameof(entry));
        this.Init(entry.CurveA, entry.CurveB, entry.CurveM, entry.Matrix3x1, entry.Matrix3x3, entry.ClutValues);
        this.type |= CalculationType.BtoA;
    }

    public Vector4 Calculate(Vector4 value)
    {
        switch (this.type)
        {
            case CalculationType.Full | CalculationType.AtoB:
                value = this.curveACalculator.Calculate(value);
                value = this.clutCalculator.Calculate(value);
                value = this.curveMCalculator.Calculate(value);
                value = this.matrixCalculator.Calculate(value);
                return this.curveBCalculator.Calculate(value);

            case CalculationType.Full | CalculationType.BtoA:
                value = this.curveBCalculator.Calculate(value);
                value = this.matrixCalculator.Calculate(value);
                value = this.curveMCalculator.Calculate(value);
                value = this.clutCalculator.Calculate(value);
                return this.curveACalculator.Calculate(value);

            case CalculationType.CurveClut | CalculationType.AtoB:
                value = this.curveACalculator.Calculate(value);
                value = this.clutCalculator.Calculate(value);
                return this.curveBCalculator.Calculate(value);

            case CalculationType.CurveClut | CalculationType.BtoA:
                value = this.curveBCalculator.Calculate(value);
                value = this.clutCalculator.Calculate(value);
                return this.curveACalculator.Calculate(value);

            case CalculationType.CurveMatrix | CalculationType.AtoB:
                value = this.curveMCalculator.Calculate(value);
                value = this.matrixCalculator.Calculate(value);
                return this.curveBCalculator.Calculate(value);

            case CalculationType.CurveMatrix | CalculationType.BtoA:
                value = this.curveBCalculator.Calculate(value);
                value = this.matrixCalculator.Calculate(value);
                return this.curveMCalculator.Calculate(value);

            case CalculationType.SingleCurve | CalculationType.AtoB:
            case CalculationType.SingleCurve | CalculationType.BtoA:
                return this.curveBCalculator.Calculate(value);

            default:
                throw new InvalidOperationException("Invalid calculation type");
        }
    }

    private void Init(IccTagDataEntry[] curveA, IccTagDataEntry[] curveB, IccTagDataEntry[] curveM, Vector3? matrix3x1, Matrix4x4? matrix3x3, IccClut clut)
    {
        bool hasACurve = curveA != null;
        bool hasBCurve = curveB != null;
        bool hasMCurve = curveM != null;
        bool hasMatrix = matrix3x1 != null && matrix3x3 != null;
        bool hasClut = clut != null;

        if (hasBCurve && hasMatrix && hasMCurve && hasClut && hasACurve)
        {
            this.type = CalculationType.Full;
        }
        else if (hasBCurve && hasClut && hasACurve)
        {
            this.type = CalculationType.CurveClut;
        }
        else if (hasBCurve && hasMatrix && hasMCurve)
        {
            this.type = CalculationType.CurveMatrix;
        }
        else if (hasBCurve)
        {
            this.type = CalculationType.SingleCurve;
        }
        else
        {
            throw new InvalidIccProfileException("AToB or BToA tag has an invalid configuration");
        }

        if (hasACurve)
        {
            this.curveACalculator = new TrcCalculator(curveA, false);
        }

        if (hasBCurve)
        {
            this.curveBCalculator = new TrcCalculator(curveB, false);
        }

        if (hasMCurve)
        {
            this.curveMCalculator = new TrcCalculator(curveM, false);
        }

        if (hasMatrix)
        {
            this.matrixCalculator = new MatrixCalculator(matrix3x3.Value, matrix3x1.Value);
        }

        if (hasClut)
        {
            this.clutCalculator = new ClutCalculator(clut);
        }
    }
}
