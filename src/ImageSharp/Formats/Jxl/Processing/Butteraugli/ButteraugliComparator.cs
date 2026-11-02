// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;

/// <summary>
/// Owns the reference frequency bands, lower-resolution comparators and reusable blur storage for Butteraugli.
/// </summary>
/// <remarks>Comparisons reuse mutable scratch and run sequentially for each comparator.</remarks>
internal sealed class ButteraugliComparator : IDisposable
{
    private readonly int xSize;
    private readonly int ySize;
    private readonly ButteraugliParameters parameters;
    /// <summary>
    /// The reference decomposition, retained unchanged across comparisons.
    /// </summary>
    private readonly ButteraugliPsychoImage pi0 = new();

    /// <summary>
    /// The distorted decomposition. Its MF and LF planes become six mask scratch planes after their last comparison.
    /// </summary>
    private readonly ButteraugliPsychoImage distorted = new();

    /// <summary>
    /// The three blurred RGB planes, reused for accumulated AC differences after the opsin transform.
    /// </summary>
    private readonly JxlImage3<float> temp = new();

    /// <summary>
    /// The transformed actual image, reused for DC differences after frequency separation.
    /// </summary>
    private readonly JxlImage3<float> transformedActual = new();

    /// <summary>
    /// Malta difference scratch, reused for the output mask after the last Malta comparison.
    /// </summary>
    private readonly JxlPlane<float> differences = new();

    /// <summary>
    /// The lower-resolution comparison result. It remains distinct from this comparator's result and mask storage.
    /// </summary>
    private readonly JxlPlane<float> subsampledDifference = new();

    private readonly ButteraugliBlurTemp blurTemp;
    private ButteraugliComparator? sub;

    /// <summary>
    /// Allocates the comparison workspace without taking ownership of input pixels.
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
        this.blurTemp = new(configuration, xsize, ysize);

        try
        {
            // All repeated-comparison storage belongs to this owner. Swapping preserves the empty wrappers used for failure cleanup.
            using JxlImage3<float> blurStorage = new(configuration, xsize, ysize);
            this.temp.Swap(blurStorage);

            using JxlImage3<float> transformedStorage = new(configuration, xsize, ysize);
            this.transformedActual.Swap(transformedStorage);

            this.pi0.Allocate(configuration, xsize, ysize);
            this.distorted.Allocate(configuration, xsize, ysize);

            using JxlPlane<float> differenceStorage = new(configuration, xsize, ysize);
            this.differences.Swap(differenceStorage);
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the borrowed three-plane workspace. Opsin uses it for blurred RGB; the next phase replaces those samples with AC differences.
    /// </summary>
    public JxlImage3<float> Temp => this.temp;

    /// <summary>
    /// Builds the owned reference decomposition and the half-resolution reference used by comparison.
    /// </summary>
    /// <param name="configuration">The configuration used for owned sample allocations.</param>
    /// <param name="rgb0">The borrowed reference pixels.</param>
    /// <param name="parameters">The perceptual comparison parameters.</param>
    /// <returns>The comparator that owns both required reference decompositions.</returns>
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

            result.InitializeReference(configuration, rgb0);

            // Smaller comparisons return only zero. Do not construct their frequency bands or blur workspace.
            if ((xSize + 1) / 2 >= 8 && (ySize + 1) / 2 >= 8)
            {
                using JxlImage3<float> subsampledRgb0 = Butteraugli.SubSample2x(configuration, rgb0);
                result.sub = new(configuration, subsampledRgb0.XSize, subsampledRgb0.YSize, parameters);
                result.sub.InitializeReference(configuration, subsampledRgb0);

                // Diffmap reads the child's frequency bands directly. It does not run the child's lower-resolution comparison.
                // Prepare only that half-size reference; quarter-size and smaller decompositions would remain unread.
                using JxlPlane<float> subResult = new(configuration, result.sub.xSize, result.sub.ySize);
                result.subsampledDifference.Swap(subResult);
            }

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
    /// Compares borrowed distorted RGB against the retained reference and writes the perceptual difference map.
    /// </summary>
    /// <param name="configuration">The configuration used by the comparison stages.</param>
    /// <param name="rgb1">The borrowed distorted pixels.</param>
    /// <param name="result">The caller-owned result plane, already allocated at the reference dimensions.</param>
    /// <returns>Whether all comparison stages succeeded.</returns>
    public bool Diffmap(Configuration configuration, JxlImage3<float> rgb1, JxlPlane<float> result)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            result.Clear();
            return true;
        }

        if (!Butteraugli.OpsinDynamicsImage(configuration, rgb1, this.parameters, this.Temp, this.blurTemp, this.transformedActual))
        {
            return false;
        }

        if (!this.DiffmapOpsinDynamicsImage(configuration, this.transformedActual, result))
        {
            return false;
        }

        if (this.sub is not null)
        {
            // Subsampling fills the child's reusable transformed image. OpsinDynamicsImage supports identical input and output.
            Butteraugli.SubSample2x(rgb1, this.sub.transformedActual);

            if (!Butteraugli.OpsinDynamicsImage(
                configuration, this.sub.transformedActual, this.parameters, this.sub.Temp, this.sub.blurTemp, this.sub.transformedActual))
            {
                return false;
            }

            // Use the lower-resolution reference decomposition with its matching lower-resolution distorted image.
            if (!this.sub.DiffmapOpsinDynamicsImage(configuration, this.sub.transformedActual, this.subsampledDifference))
            {
                return false;
            }

            Butteraugli.AddSupersampled2x(this.subsampledDifference, 0.5f, result);
        }

        return true;
    }

    /// <summary>
    /// Computes the comparison from borrowed samples that already passed through the opsin dynamics transform.
    /// </summary>
    /// <param name="configuration">The configuration used by the comparison stages.</param>
    /// <param name="xyb1">The borrowed transformed samples.</param>
    /// <param name="result">The caller-owned result plane, already allocated at the reference dimensions.</param>
    /// <returns>Whether frequency separation and comparison succeeded.</returns>
    public bool DiffmapOpsinDynamicsImage(Configuration configuration, JxlImage3<float> xyb1, JxlPlane<float> result)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            result.Clear();
            return true;
        }

        if (!Butteraugli.SeparateFrequencies(configuration, this.blurTemp, xyb1, this.distorted))
        {
            return false;
        }

        return this.DiffmapPsychoImage(configuration, this.distorted, result);
    }

    /// <summary>
    /// Compares a borrowed frequency decomposition against the owned reference decomposition.
    /// </summary>
    /// <param name="configuration">The configuration used by the comparison stages.</param>
    /// <param name="pi1">The borrowed distorted frequency bands.</param>
    /// <param name="diffmap">The caller-owned result plane, already allocated at the reference dimensions.</param>
    /// <returns>Whether every comparison and mask stage succeeded.</returns>
    public bool DiffmapPsychoImage(Configuration configuration, ButteraugliPsychoImage pi1, JxlPlane<float> diffmap)
    {
        if (this.xSize < 8 || this.ySize < 8)
        {
            diffmap.Clear();
            return true;
        }

        float hfAsymmetry = this.parameters.HfAsymmetry;
        float xmul = this.parameters.XMultiplier;

        // Opsin no longer needs blurred RGB, and frequency separation has consumed transformedActual.
        // Reuse those allocations for AC accumulation and fully written DC differences. Only AC requires clearing.
        JxlPlane<float> diffs = this.differences;
        JxlImage3<float> blockDiffAc = this.temp;

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

        // Native MF comparison uses its MF weight in both directions. HF asymmetry applies only to the HF and UHF bands.
        if (!Butteraugli.MaltaDiffMapLf(
            this.pi0.Mf.Plane(1),
            pi1.Mf.Plane(1),
            Butteraugli.WMfMalta,
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
            Butteraugli.WMfMaltaX,
            Butteraugli.WMfMaltaX,
            Butteraugli.Norm1MfX,
            diffs,
            blockDiffAc,
            0))
        {
            return false;
        }

        JxlImage3<float> blockDiffDc = this.transformedActual;

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

        // All MF and LF samples have now contributed to their differences. Reuse their six planes as mask scratch.
        // Hf and Uhf remain intact until masking reads them; Malta scratch can now hold the completed mask.
        JxlPlane<float> mask = this.differences;

        if (!Butteraugli.MaskButteraugliPsychoImage(
            configuration,
            this.pi0,
            pi1,
            this.xSize,
            this.ySize,
            this.blurTemp,
            mask,
            blockDiffAc.Plane(1),
            this.distorted.Mf.Plane(0),
            this.distorted.Mf.Plane(1),
            this.distorted.Mf.Plane(2),
            this.distorted.Lf.Plane(0),
            this.distorted.Lf.Plane(1),
            this.distorted.Lf.Plane(2)))
        {
            return false;
        }

        return Butteraugli.CombineChannelsToDiffmap(mask, blockDiffDc, blockDiffAc, xmul, diffmap);
    }

    /// <summary>
    /// Writes the reference image's perceptual mask into caller-owned storage.
    /// </summary>
    /// <param name="configuration">The configuration used by the mask stages.</param>
    /// <param name="mask">The caller-owned output plane, already allocated at the reference dimensions.</param>
    /// <returns>Whether mask construction succeeded.</returns>
    public bool Mask(Configuration configuration, JxlPlane<float> mask)
        => Butteraugli.MaskButteraugliPsychoImage(
            configuration,
            this.pi0,
            this.pi0,
            this.xSize,
            this.ySize,
            this.blurTemp,
            mask,
            null,
            this.distorted.Mf.Plane(0),
            this.distorted.Mf.Plane(1),
            this.distorted.Mf.Plane(2),
            this.distorted.Lf.Plane(0),
            this.distorted.Lf.Plane(1),
            this.distorted.Lf.Plane(2));

    /// <inheritdoc />
    public void Dispose()
    {
        this.temp.Dispose();
        this.transformedActual.Dispose();
        this.distorted.Dispose();
        this.differences.Dispose();
        this.subsampledDifference.Dispose();
        this.pi0.Dispose();
        this.sub?.Dispose();
        this.sub = null;
        this.blurTemp.Dispose();
    }

    /// <summary>
    /// Prepares this comparator's reference bands in its existing buffers without constructing another resolution level.
    /// </summary>
    /// <param name="configuration">The configuration used by the comparison stages.</param>
    /// <param name="rgb">The borrowed reference, with the dimensions supplied to this comparator's constructor.</param>
    private void InitializeReference(Configuration configuration, JxlImage3<float> rgb)
    {
        if (!Butteraugli.OpsinDynamicsImage(configuration, rgb, this.parameters, this.Temp, this.blurTemp, this.transformedActual))
        {
            throw new InvalidOperationException("OpsinDynamicsImage failed");
        }

        if (!Butteraugli.SeparateFrequencies(configuration, this.blurTemp, this.transformedActual, this.pi0))
        {
            throw new InvalidOperationException("Could not separate frequencies");
        }
    }
}
