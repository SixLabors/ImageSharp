// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

internal struct JxlCustomTransferFunction
{
    private const uint MaxGamma = 8192;
    private const uint GammaMultiplier = 10000000;

    public JxlCustomTransferFunction()
    {
    }

    public bool HaveGamma { get; set; }

    public uint Gamma { get; set; }

    public JxlTransferFunction TransferFunction { get; set; } = JxlTransferFunction.SRgb;

    public readonly bool IsUnknown => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Unknown;

    public readonly bool IsSrgb => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.SRgb;

    public readonly bool IsLinear => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Linear;

    public readonly bool IsPq => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Pq;

    public readonly bool IsHlg => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Hlg;

    public readonly bool Is709 => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Bt709;

    public readonly bool IsDci => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Dci;

    public readonly JxlTransferFunction GetTransferFunction()
    {
        if (this.HaveGamma)
        {
            return JxlTransferFunction.Unknown;
        }

        return this.TransferFunction;
    }

    public void SetTransferFunction(JxlTransferFunction tf)
    {
        this.HaveGamma = false;
        this.TransferFunction = tf;
    }

    public readonly float GetGamma()
    {
        if (!this.HaveGamma)
        {
            return 0.0f;
        }

        return this.Gamma * (1.0f / GammaMultiplier);
    }

    public void SetGamma(float newGamma)
    {
        if (newGamma is < 1.0f / MaxGamma or > 1.0f)
        {
            throw new InvalidOperationException($"Invalid gamma {newGamma}");
        }

        this.HaveGamma = false;

        if (IsAlmostEqual(newGamma, 1.0f))
        {
            this.TransferFunction = JxlTransferFunction.Linear;
            return;
        }

        if (IsAlmostEqual(newGamma, 1.0f / 2.6f))
        {
            this.TransferFunction = JxlTransferFunction.Dci;
            return;
        }

        // Don't translate 0.45.. to kSRGB nor k709 - that might change pixel
        // values because those curves also have a linear part.
        this.HaveGamma = true;
        this.Gamma = (uint)MathF.Round((float)(newGamma * GammaMultiplier));
        this.TransferFunction = JxlTransferFunction.Unknown;
    }

    public readonly bool IsSame(JxlCustomTransferFunction other)
    {
        if (this.HaveGamma != other.HaveGamma)
        {
            return false;
        }

        if (this.HaveGamma)
        {
            return this.Gamma == other.Gamma;
        }

        return this.TransferFunction == other.TransferFunction;
    }

    private static bool IsAlmostEqual(float a, float b)
    {
        const float dist = 1e-3f;
        return MathF.Abs(a - b) < dist;
    }
}
