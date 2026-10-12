// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

internal class ButteraugliComparator : IDisposable
{
    private readonly int xSize;
    private readonly int ySize;
    private ButteraugliParameters parameters;
    private readonly ButteraugliPsychoImage pi0 = new();
    private JxlImage3F? temp;
    private readonly ButteraugliBlurTemp blurTemp = new();
    private ButteraugliComparator? sub;

    public ButteraugliComparator(int xsize, int ysize, ButteraugliParameters parameters)
    {
        this.xSize = xsize;
        this.ySize = ysize;
        this.parameters = parameters;
    }

    public JxlImage3F Temp => this.temp ??= new();

    public static ButteraugliComparator Make(Configuration configuration, JxlImage3F rgb0, ButteraugliParameters parameters)
    {
        int xSize = rgb0.XSize;
        int ySize = rgb0.YSize;

        ButteraugliComparator result = new(xSize, ySize, parameters)
        {
            temp = new JxlImage3F(configuration, xSize, ySize)
        };

        if (xSize < 8 || ySize < 8)
        {
            return result;
        }

        JxlImage3F xyb0 = new(configuration, xSize, ySize);

        if (!Butteraugli.OpsinDynamicsImage(configuration, rgb0, parameters, result.Temp, result.blurTemp, xyb0))
        {
            throw new InvalidOperationException("OpsinDynamicsImage failed");
        }

        result.ReleaseTemp();

        if (!Butteraugli.SeparateFrequencies(configuration, result.blurTemp, xyb0, result.pi0))
        {
            throw new InvalidOperationException("Could not separate frequencies");
        }

        JxlImage3F subsampledRgb0 = Butteraugli.SubSample2x(configuration, rgb0);
        result.sub = Make(configuration, subsampledRgb0, parameters);

        return result;
    }

    public void ReleaseTemp()
    {
        this.temp?.Dispose();
        this.temp = null;
    }

    /// <summary>
    /// Computes the butteraugli map between the original image given in the constructor and the distorted image given here.
    /// </summary>
    public virtual bool Diffmap(Configuration configuration, JxlImage3F rgb1, JxlImageF result)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            result.Clear();
            return true;
        }

        JxlImage3F xyb1 = new(configuration, this.xSize, this.ySize);

        if (!Butteraugli.OpsinDynamicsImage(configuration, rgb1, this.parameters, this.Temp, this.blurTemp, xyb1))
        {
            return false;
        }

        this.ReleaseTemp();

        if (!this.DiffmapOpsinDynamicsImage(configuration, xyb1, out result))
        {
            return false;
        }

        if (this.sub is not null)
        {
            if (this.sub.xSize < 8 || this.sub.ySize < 8)
            {
                return true;
            }

            JxlImage3F subXyb = new(configuration, this.sub.xSize, this.sub.ySize);
            JxlImage3F subsampledRgb1 = Butteraugli.SubSample2x(configuration, rgb1);

            if (!Butteraugli.OpsinDynamicsImage(configuration, subsampledRgb1, this.parameters, this.sub.Temp, this.sub.blurTemp, subXyb))
            {
                return false;
            }

            this.sub.ReleaseTemp();

            if (!this.DiffmapOpsinDynamicsImage(configuration, subXyb, out JxlImageF subResult))
            {
                return false;
            }

            Butteraugli.AddSupersampled2x(subResult, 0.5f, result);
        }

        return true;
    }

    /// <summary>
    /// Same as Diffmap but OpsinDynamicsImage() was already applied.
    /// </summary>
    public bool DiffmapOpsinDynamicsImage(Configuration configuration, JxlImage3F xyb1, out JxlImageF result)
    {
        result = new();

        if (this.xSize < 8 || this.ySize < 8)
        {
            result.Clear();
            return true;
        }

        ButteraugliPsychoImage pi1 = new();

        if (!Butteraugli.SeparateFrequencies(configuration, this.blurTemp, xyb1, pi1))
        {
            return false;
        }

        result = new(configuration, this.xSize, this.ySize);
        return this.DiffmapPsychoImage(configuration, pi1, result);
    }

    /// <summary>
    /// Same as above but the frequency decomposition was already applied.
    /// </summary>
    public bool DiffmapPsychoImage(Configuration configuration, ButteraugliPsychoImage pi1, JxlImageF diffmap)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            diffmap.Clear();
            return true;
        }

        float hfAsymmetry = this.parameters.HfAsymmetry;
        float xmul = this.parameters.XMultiplier;

        JxlImageF diffs = new(configuration, this.xSize, this.ySize);
        JxlImage3<float> blockDiffAc = new(configuration, this.xSize, this.ySize);

        JxlImageOperations.ZeroFillImage(blockDiffAc);

        if (!Butteraugli.MaltaDiffMap(
            this.pi0.Uhf[1],
            pi1.Uhf[1],
            Butteraugli.WUhfMalta * hfAsymmetry,
            Butteraugli.WUhfMalta / hfAsymmetry,
            Butteraugli.Norm1Uhf,
            diffs,
            blockDiffAc,
            1))
        {
            return false;
        }

        if (!Butteraugli.MaltaDiffMap(
            this.pi0.Uhf[0],
            pi1.Uhf[0],
            Butteraugli.WUhfMaltaX * hfAsymmetry,
            Butteraugli.WUhfMaltaX / hfAsymmetry,
            Butteraugli.Norm1UhfX,
            diffs,
            blockDiffAc,
            0))
        {
            return false;
        }

        if (!Butteraugli.MaltaDiffMapLf(
            this.pi0.Hf[1],
            pi1.Hf[1],
            Butteraugli.WHfMalta * MathF.Sqrt(hfAsymmetry),
            Butteraugli.WHfMalta / MathF.Sqrt(hfAsymmetry),
            Butteraugli.Norm1Hf,
            diffs,
            blockDiffAc,
            1))
        {
            return false;
        }

        if (!Butteraugli.MaltaDiffMapLf(
            this.pi0.Hf[0],
            pi1.Hf[0],
            Butteraugli.WHfMaltaX * MathF.Sqrt(hfAsymmetry),
            Butteraugli.WHfMaltaX / MathF.Sqrt(hfAsymmetry),
            Butteraugli.Norm1HfX,
            diffs,
            blockDiffAc,
            0))
        {
            return false;
        }

        if (!Butteraugli.MaltaDiffMapLf(
            this.pi0.Mf!.Plane(1),
            pi1.Mf!.Plane(1),
            Butteraugli.WHfMalta,
            Butteraugli.WMfMalta,
            Butteraugli.Norm1Mf,
            diffs,
            blockDiffAc,
            1))
        {
            return false;
        }

        if (!Butteraugli.MaltaDiffMapLf(
            this.pi0.Mf!.Plane(0),
            pi1.Mf!.Plane(0),
            Butteraugli.WHfMaltaX,
            Butteraugli.WMfMaltaX,
            Butteraugli.Norm1MfX,
            diffs,
            blockDiffAc,
            0))
        {
            return false;
        }

        JxlImage3F blockDiffDc = new(configuration, this.xSize, this.ySize);

        for (int c = 0; c < 3; c++)
        {
            if (c < 2)
            {
                Butteraugli.L2DiffAsymmetric(
                    this.pi0.Hf[c],
                    pi1.Hf[c],
                    Butteraugli.Wmul[c] * hfAsymmetry,
                    Butteraugli.Wmul[c] / hfAsymmetry,
                    blockDiffAc.Plane(c));
            }

            Butteraugli.L2Diff(
                this.pi0.Mf.Plane(c),
                pi1.Mf.Plane(c),
                Butteraugli.Wmul[3 + c],
                blockDiffAc.Plane(c));

            Butteraugli.SetL2Diff(
                this.pi0.Lf!.Plane(c),
                pi1.Lf!.Plane(c),
                Butteraugli.Wmul[6 + c],
                blockDiffDc.Plane(c));
        }

        JxlImageF mask = new();

        if (!Butteraugli.MaskButteraugliPsychoImage(
            configuration,
            this.pi0,
            pi1,
            this.xSize,
            this.ySize,
            this.blurTemp,
            mask,
            out JxlImageF? diffAc))
        {
            return false;
        }

        diffAc?.BytesSpan.CopyTo(blockDiffAc.Plane(1).BytesSpan);

        return Butteraugli.CombineChannelsToDiffmap(mask, blockDiffDc, blockDiffAc, xmul, diffmap);
    }

    public virtual bool Mask(Configuration configuration, JxlImageF mask)
        => Butteraugli.MaskButteraugliPsychoImage(configuration, this.pi0, this.pi0, this.xSize, this.ySize, this.blurTemp, mask, out _);

    public void Dispose()
    {
        this.temp?.Dispose();
        this.temp = null;
        this.blurTemp.Dispose();
    }
}
