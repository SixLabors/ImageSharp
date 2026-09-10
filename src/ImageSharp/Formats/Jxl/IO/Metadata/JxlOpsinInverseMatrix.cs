// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1401 // Fields should be private

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlOpsinInverseMatrix : IJxlFields
{
    private InlineArray3<float> opsinBiases;
    private InlineArray3<float> quantBiases;
    private bool allDefault;
    private JxlMatrix3x3F inverseMatrix;

    public InlineArray3<float> OpsinBiases
    {
        get => this.opsinBiases;
        set => this.opsinBiases = value;
    }

    public InlineArray3<float> QuantBiases
    {
        get => this.quantBiases;
        set => this.quantBiases = value;
    }

    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    public JxlMatrix3x3F InverseMatrix
    {
        get => this.inverseMatrix;
        set => this.inverseMatrix = value;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            // Overwrite all serialized fields, but not any nonserialized_*.
            visitor.SetDefault(this);
            return true;
        }

        JxlMatrix3x3F defaultInverse = JxlCms.DefaultInverseOpsinAbsorbanceMatrix();

        for (int j = 0; j < 3; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                if (!visitor.F16(defaultInverse[j][i], ref this.inverseMatrix[j][i]))
                {
                    return false;
                }
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (!visitor.F16(JxlCms.NegOpsinAbsorbanceBiasRgb[i], ref this.opsinBiases[i]))
            {
                return false;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (!visitor.F16(DefaultQuantBias[i], ref this.quantBiases[i]))
            {
                return false;
            }
        }

        return true;
    }
}
