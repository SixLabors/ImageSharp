// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Stores a named transfer function or the exact integer gamma field from a JPEG XL header.
/// </summary>
internal struct JxlCustomTransferFunction
{
    private const uint MaxGamma = 8192;
    private const uint GammaMultiplier = 10000000;
    private const uint XybGamma = GammaMultiplier / 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlCustomTransferFunction"/> struct with sRGB transfer defaults.
    /// </summary>
    public JxlCustomTransferFunction()
    {
    }

    /// <summary>
    /// Gets or sets whether the encoding uses a custom gamma instead of a named transfer function.
    /// </summary>
    public bool HaveGamma { get; set; }

    /// <summary>
    /// Gets or sets the OETF exponent multiplied by ten million. Direct wire decoding preserves this integer.
    /// </summary>
    public uint Gamma { get; set; } = GammaMultiplier;

    /// <summary>
    /// Gets or sets the named transfer function used when <see cref="HaveGamma"/> is false.
    /// </summary>
    public JxlTransferFunction TransferFunction { get; set; } = JxlTransferFunction.SRgb;

    public readonly bool IsUnknown => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Unknown;

    public readonly bool IsSrgb => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.SRgb;

    public readonly bool IsLinear => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Linear;

    public readonly bool IsPq => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Pq;

    public readonly bool IsHlg => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Hlg;

    public readonly bool Is709 => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Bt709;

    public readonly bool IsDci => !this.HaveGamma && this.TransferFunction == JxlTransferFunction.Dci;

    /// <summary>
    /// Visits the named transfer function or exact integer gamma for the current color space.
    /// </summary>
    /// <param name="visitor">The header field operation.</param>
    /// <param name="colorSpace">The color space that determines whether transfer fields are implicit.</param>
    /// <returns>Whether the transfer fields satisfy the wire encoding and gamma bounds.</returns>
    public bool Visit(JxlVisitor visitor, JxlColorSpace colorSpace)
    {
        bool implicitTransfer = colorSpace == JxlColorSpace.Xyb;

        if (implicitTransfer)
        {
            // XYB has an implicit one-third exponent. It consumes no transfer bits.
            // Keep the encoded integer approximation, rather than converting through a floating-point setter.
            this.HaveGamma = true;
            this.Gamma = XybGamma;
            this.TransferFunction = JxlTransferFunction.Unknown;
        }

        if (visitor.Conditional(!implicitTransfer))
        {
            bool haveGamma = this.HaveGamma;

            if (!visitor.Boolean(false, ref haveGamma))
            {
                return false;
            }

            this.HaveGamma = haveGamma;

            if (visitor.Conditional(haveGamma))
            {
                uint gamma = this.Gamma;

                if (!visitor.Bits(24, GammaMultiplier, ref gamma))
                {
                    return false;
                }

                // The OETF exponent is gamma / 10000000 and must be within [1/8192,1].
                // Integer validation preserves the exact decoded value and cannot round an invalid exponent into range.
                if (gamma > GammaMultiplier || ((ulong)gamma * MaxGamma) < GammaMultiplier)
                {
                    return false;
                }

                this.Gamma = gamma;
            }

            if (visitor.Conditional(!haveGamma))
            {
                JxlTransferFunction transferFunction = this.TransferFunction;

                if (!visitor.Enum(JxlTransferFunction.SRgb, ref transferFunction))
                {
                    return false;
                }

                this.TransferFunction = transferFunction;
            }
        }

        return true;
    }

    public readonly JxlTransferFunction GetTransferFunction()
    {
        if (this.HaveGamma)
        {
            return JxlTransferFunction.Unknown;
        }

        return this.TransferFunction;
    }

    /// <summary>
    /// Selects a named transfer function instead of a custom gamma.
    /// </summary>
    /// <param name="transferFunction">The transfer function to select.</param>
    public void SetTransferFunction(JxlTransferFunction transferFunction)
    {
        this.HaveGamma = false;
        this.TransferFunction = transferFunction;
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
