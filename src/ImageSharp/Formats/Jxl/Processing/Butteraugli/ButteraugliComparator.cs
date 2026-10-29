// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

/// <summary>
/// Owns the reference frequency bands, lower-resolution comparators and reusable blur storage for Butteraugli.
/// </summary>
/// <remarks>Comparisons reuse mutable scratch and run sequentially for each comparator.</remarks>
internal class ButteraugliComparator : IDisposable
{
    private readonly int xSize;
    private readonly int ySize;
    private readonly ButteraugliParameters parameters;
    private readonly ButteraugliPsychoImage pi0 = new();
    private readonly JxlImage3<float> temp;
    private readonly ButteraugliBlurTemp blurTemp = new();
    private ButteraugliComparator? sub;

    /// <summary>
    /// Initializes a comparator and its full-resolution scratch image without taking ownership of input pixels.
    /// </summary>
    /// <param name="configuration">The configuration used for owned sample allocations.</param>
    /// <param name="xsize">The comparison width.</param>
    /// <param name="ysize">The comparison height.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    public ButteraugliComparator(Configuration configuration, int xsize, int ysize, ButteraugliParameters parameters)
    {
        this.xSize = xsize;
        this.ySize = ysize;
        this.parameters = parameters;
        this.temp = new(configuration, xsize, ysize);
    }

    /// <summary>
    /// Gets the borrowed blur scratch image. The comparator retains this allocation between comparisons.
    /// </summary>
    public JxlImage3<float> Temp => this.temp;

    /// <summary>
    /// Builds the owned reference decomposition and its lower-resolution comparisons from borrowed RGB samples.
    /// </summary>
    /// <param name="configuration">The configuration used for owned sample allocations.</param>
    /// <param name="rgb0">The borrowed reference pixels.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <returns>The comparator that owns the completed reference decomposition.</returns>
    public static ButteraugliComparator Make(Configuration configuration, JxlImage3<float> rgb0, ButteraugliParameters parameters)
    {
        int xSize = rgb0.XSize;
        int ySize = rgb0.YSize;

        ButteraugliComparator result = new(configuration, xSize, ySize, parameters);

        try
        {
            if (xSize < 8 || ySize < 8)
            {
                return result;
            }

            using JxlImage3<float> xyb0 = new(configuration, xSize, ySize);

            if (!Butteraugli.OpsinDynamicsImage(configuration, rgb0, parameters, result.Temp, result.blurTemp, xyb0))
            {
                throw new InvalidOperationException("OpsinDynamicsImage failed");
            }

            if (!Butteraugli.SeparateFrequencies(configuration, result.blurTemp, xyb0, result.pi0))
            {
                throw new InvalidOperationException("Could not separate frequencies");
            }

            using JxlImage3<float> subsampledRgb0 = Butteraugli.SubSample2x(configuration, rgb0);
            result.sub = Make(configuration, subsampledRgb0, parameters);

            return result;
        }
        catch
        {
            // Construction publishes ownership only after every required frequency band and lower-resolution comparator succeeds.
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Computes the butteraugli map between the original image given in the constructor and the distorted image given here.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary sample allocations.</param>
    /// <param name="rgb1">The borrowed distorted pixels.</param>
    /// <param name="result">The caller-owned result plane. Its storage is resized or reused to match the reference.</param>
    /// <returns>Whether all comparison stages succeeded.</returns>
    public virtual bool Diffmap(Configuration configuration, JxlImage3<float> rgb1, JxlPlane<float> result)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            this.PrepareResult(configuration, result);
            result.Clear();
            return true;
        }

        using JxlImage3<float> xyb1 = new(configuration, this.xSize, this.ySize);

        if (!Butteraugli.OpsinDynamicsImage(configuration, rgb1, this.parameters, this.Temp, this.blurTemp, xyb1))
        {
            return false;
        }

        if (!this.DiffmapOpsinDynamicsImage(configuration, xyb1, result))
        {
            return false;
        }

        if (this.sub is not null)
        {
            if (this.sub.xSize < 8 || this.sub.ySize < 8)
            {
                return true;
            }

            using JxlImage3<float> subXyb = new(configuration, this.sub.xSize, this.sub.ySize);
            using JxlImage3<float> subsampledRgb1 = Butteraugli.SubSample2x(configuration, rgb1);

            if (!Butteraugli.OpsinDynamicsImage(configuration, subsampledRgb1, this.parameters, this.sub.Temp, this.sub.blurTemp, subXyb))
            {
                return false;
            }

            using JxlPlane<float> subResult = new();

            // Use the lower-resolution reference decomposition with its matching lower-resolution distorted image.
            if (!this.sub.DiffmapOpsinDynamicsImage(configuration, subXyb, subResult))
            {
                return false;
            }

            Butteraugli.AddSupersampled2x(subResult, 0.5f, result);
        }

        return true;
    }

    /// <summary>
    /// Computes the comparison from borrowed samples that already passed through the opsin dynamics transform.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary sample allocations.</param>
    /// <param name="xyb1">The borrowed transformed samples.</param>
    /// <param name="result">The caller-owned result plane.</param>
    /// <returns>Whether frequency separation and comparison succeeded.</returns>
    public bool DiffmapOpsinDynamicsImage(Configuration configuration, JxlImage3<float> xyb1, JxlPlane<float> result)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            this.PrepareResult(configuration, result);
            result.Clear();
            return true;
        }

        using ButteraugliPsychoImage pi1 = new();

        if (!Butteraugli.SeparateFrequencies(configuration, this.blurTemp, xyb1, pi1))
        {
            return false;
        }

        return this.DiffmapPsychoImage(configuration, pi1, result);
    }

    /// <summary>
    /// Compares a borrowed frequency decomposition against the owned reference decomposition.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary sample allocations.</param>
    /// <param name="pi1">The borrowed distorted frequency bands.</param>
    /// <param name="diffmap">The caller-owned result plane.</param>
    /// <returns>Whether every comparison and mask stage succeeded.</returns>
    public bool DiffmapPsychoImage(Configuration configuration, ButteraugliPsychoImage pi1, JxlPlane<float> diffmap)
    {
        this.PrepareResult(configuration, diffmap);

        if (this.xSize < 8 || this.ySize < 8)
        {
            diffmap.Clear();
            return true;
        }

        float hfAsymmetry = this.parameters.HfAsymmetry;
        float xmul = this.parameters.XMultiplier;

        using JxlPlane<float> diffs = new(configuration, this.xSize, this.ySize);
        using JxlImage3<float> blockDiffAc = new(configuration, this.xSize, this.ySize);

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
            this.pi0.Mf.Plane(1),
            pi1.Mf.Plane(1),
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
            this.pi0.Mf.Plane(0),
            pi1.Mf.Plane(0),
            Butteraugli.WHfMaltaX,
            Butteraugli.WMfMaltaX,
            Butteraugli.Norm1MfX,
            diffs,
            blockDiffAc,
            0))
        {
            return false;
        }

        using JxlImage3<float> blockDiffDc = new(configuration, this.xSize, this.ySize);

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
                this.pi0.Lf.Plane(c),
                pi1.Lf.Plane(c),
                Butteraugli.Wmul[6 + c],
                blockDiffDc.Plane(c));
        }

        using JxlPlane<float> mask = new();

        if (!Butteraugli.MaskButteraugliPsychoImage(configuration, this.pi0, pi1, this.xSize, this.ySize, this.blurTemp, mask, blockDiffAc.Plane(1)))
        {
            return false;
        }

        return Butteraugli.CombineChannelsToDiffmap(mask, blockDiffDc, blockDiffAc, xmul, diffmap);
    }

    /// <summary>
    /// Writes the reference image's perceptual mask into caller-owned storage.
    /// </summary>
    /// <param name="configuration">The configuration used for temporary sample allocations.</param>
    /// <param name="mask">The caller-owned output plane.</param>
    /// <returns>Whether mask construction succeeded.</returns>
    public virtual bool Mask(Configuration configuration, JxlPlane<float> mask)
        => Butteraugli.MaskButteraugliPsychoImage(configuration, this.pi0, this.pi0, this.xSize, this.ySize, this.blurTemp, mask, null);

    /// <inheritdoc />
    public void Dispose()
    {
        this.temp.Dispose();
        this.pi0.Dispose();
        this.sub?.Dispose();
        this.sub = null;
        this.blurTemp.Dispose();
    }

    /// <summary>
    /// Reuses the caller's result capacity or exchanges it for an allocation matching the reference dimensions.
    /// </summary>
    /// <param name="configuration">The configuration used if a larger allocation is required.</param>
    /// <param name="result">The result plane whose identity and ownership remain with the caller.</param>
    private void PrepareResult(Configuration configuration, JxlPlane<float> result)
    {
        if (!result.ShrinkTo(this.xSize, this.ySize))
        {
            using JxlPlane<float> replacement = new(configuration, this.xSize, this.ySize);
            result.Swap(replacement);
        }
    }
}
