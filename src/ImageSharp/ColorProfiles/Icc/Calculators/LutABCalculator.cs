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

    /// <summary>
    /// Initializes a new instance of the <see cref="LutABCalculator"/> class for an ICC <c>mAB</c> transform.
    /// </summary>
    /// <param name="entry">The parsed A-to-B LUT entry.</param>
    public LutABCalculator(IccLutAToBTagDataEntry entry)
    {
        Guard.NotNull(entry, nameof(entry));
        this.Init(entry.CurveA, entry.CurveB, entry.CurveM, entry.Matrix3x1, entry.Matrix3x3, entry.ClutValues);
        this.type = CalculationType.AtoB;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LutABCalculator"/> class for an ICC <c>mBA</c> transform.
    /// </summary>
    /// <param name="entry">The parsed B-to-A LUT entry.</param>
    public LutABCalculator(IccLutBToATagDataEntry entry)
    {
        Guard.NotNull(entry, nameof(entry));
        this.Init(entry.CurveA, entry.CurveB, entry.CurveM, entry.Matrix3x1, entry.Matrix3x3, entry.ClutValues);
        this.type = CalculationType.BtoA;
    }

    /// <summary>
    /// Calculates the transformed value by applying the configured ICC LUT stages in specification order.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <returns>The transformed value.</returns>
    public Vector4 Calculate(Vector4 value)
    {
        switch (this.type)
        {
            case CalculationType.AtoB:
                // ICC mAB order: A, CLUT, M, Matrix, B.
                if (this.curveACalculator != null)
                {
                    value = this.curveACalculator.Calculate(value);
                }

                if (this.clutCalculator != null)
                {
                    value = this.clutCalculator.Calculate(value);
                }

                if (this.curveMCalculator != null)
                {
                    value = this.curveMCalculator.Calculate(value);
                }

                if (this.matrixCalculator != null)
                {
                    value = this.matrixCalculator.Calculate(value);
                }

                if (this.curveBCalculator != null)
                {
                    value = this.curveBCalculator.Calculate(value);
                }

                return value;

            case CalculationType.BtoA:
                // ICC mBA order: B, Matrix, M, CLUT, A.
                if (this.curveBCalculator != null)
                {
                    value = this.curveBCalculator.Calculate(value);
                }

                if (this.matrixCalculator != null)
                {
                    value = this.matrixCalculator.Calculate(value);
                }

                if (this.curveMCalculator != null)
                {
                    value = this.curveMCalculator.Calculate(value);
                }

                if (this.clutCalculator != null)
                {
                    value = this.clutCalculator.Calculate(value);
                }

                if (this.curveACalculator != null)
                {
                    value = this.curveACalculator.Calculate(value);
                }

                return value;

            default:
                throw new InvalidOperationException("Invalid calculation type");
        }
    }

    /// <summary>
    /// Creates calculators for the processing stages present in the LUT entry.
    /// </summary>
    /// <remarks>
    /// The tag entry classes already validate channel continuity, so this method only materializes the available stages.
    /// </remarks>
    private void Init(IccTagDataEntry[] curveA, IccTagDataEntry[] curveB, IccTagDataEntry[] curveM, Vector3? matrix3x1, Matrix4x4? matrix3x3, IccClut clut)
    {
        bool hasACurve = curveA != null;
        bool hasBCurve = curveB != null;
        bool hasMCurve = curveM != null;
        bool hasMatrix = matrix3x1 != null && matrix3x3 != null;
        bool hasClut = clut != null;

        Guard.IsTrue(
            hasACurve || hasBCurve || hasMCurve || hasMatrix || hasClut,
            nameof(curveB),
            "AToB or BToA tag must contain at least one processing element");

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
