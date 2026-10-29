// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

/// <summary>
/// Compares encoder samples against a retained reference using Butteraugli.
/// </summary>
/// <param name="parameters">The perceptual comparison parameters.</param>
/// <param name="cms">The color conversion provider for the source samples.</param>
internal sealed class JxlComparator(ButteraugliParameters parameters, JxlCmsInterface cms) : IDisposable
{
    private int width;
    private int height;
    private float intensityTarget;
    private ButteraugliComparator? comparator;

    /// <summary>
    /// Gets the score used for the encoder's good-quality target.
    /// </summary>
    public float GoodQualityScore => Butteraugli.Butteraugli.ButteraugliFuzzyInverse(1.5f);

    /// <summary>
    /// Gets the score used for the encoder's bad-quality target.
    /// </summary>
    public float BadQualityScore => Butteraugli.Butteraugli.ButteraugliFuzzyInverse(0.5f);

    /// <summary>
    /// Compares borrowed samples with the current reference and writes the difference map into reusable storage.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary sample storage.</param>
    /// <param name="actual">The borrowed image to compare without changing its samples.</param>
    /// <param name="diffMap">The caller-owned map allocated at the reference dimensions.</param>
    /// <param name="score">The computed perceptual difference.</param>
    public void CompareWith(Configuration configuration, JxlImageBundle actual, JxlPlane<float> diffMap, out float score)
    {
        DebugGuard.NotNull(this.comparator, nameof(this.comparator));

        if (this.width != actual.XSize || this.height != actual.YSize)
        {
            throw new InvalidOperationException("Image must have same size");
        }

        JxlImageMetadata metadata = actual.Metadata;
        using JxlImageBundle store = new(metadata);

        // The selected source can alias the caller's bundle. Only store owns temporary converted planes.
        JxlImageBundle actualLinearSRgb = actual;
        if (!JxlImageBundleEncoder.TransformIfNeeded(configuration, actual, JxlColorEncoding.LinearSrgb(actual.IsGray), cms, store, ref actualLinearSRgb))
        {
            throw new InvalidOperationException("Could not convert the comparison image to linear sRGB.");
        }

        // The caller allocates its map before a search. Every comparison overwrites the logical samples without exchanging storage.
        bool scaleIntensity = this.intensityTarget != 0 && actual.Metadata.IntensityTarget != this.intensityTarget;
        using JxlImage3<float>? scaledStorage = scaleIntensity && ReferenceEquals(actualLinearSRgb, actual)
            ? new(configuration, this.width, this.height)
            : null;

        JxlImage3<float> scaledActualLinearSRgb = scaledStorage ?? actualLinearSRgb.Color!;

        if (scaleIntensity)
        {
            // Reuse converted planes when store owns them. A borrowed caller image needs separate storage before scaling.
            float scale = actual.Metadata.IntensityTarget / this.intensityTarget;
            for (int c = 0; c < 3; ++c)
            {
                JxlPlane<float> source = actualLinearSRgb.Color!.Plane(c);
                JxlPlane<float> destination = scaledActualLinearSRgb.Plane(c);
                ReadOnlySpan<float> sourceSamples = source.GetRow(0);
                Span<float> destinationSamples = destination.GetRow(0);

                for (int y = 0; y < this.height; ++y)
                {
                    // Strides include padding. Select only the logical samples without acquiring Memory.Span for each row.
                    ReadOnlySpan<float> sourceRow = sourceSamples.Slice(y * source.PixelsPerRow, this.width);
                    Span<float> scaledRow = destinationSamples.Slice(y * destination.PixelsPerRow, this.width);
                    TensorPrimitives.Multiply(sourceRow, scale, scaledRow);
                }
            }
        }

        if (!this.comparator.Diffmap(configuration, scaledActualLinearSRgb, diffMap))
        {
            throw new InvalidOperationException("Diffmap failed");
        }

        score = Butteraugli.Butteraugli.ButteraugliScoreFromDiffmap(diffMap, parameters);
    }

    /// <summary>
    /// Replaces the owned reference decomposition using borrowed source samples.
    /// </summary>
    /// <param name="configuration">The configuration that owns reference storage.</param>
    /// <param name="reference">The borrowed reference samples.</param>
    public void SetReferenceImage(Configuration configuration, JxlImageBundle reference)
    {
        JxlImageMetadata metadata = reference.Metadata;
        using JxlImageBundle store = new(metadata);

        JxlImageBundle refLinearSRgb = reference;
        if (!JxlImageBundleEncoder.TransformIfNeeded(configuration, reference, JxlColorEncoding.LinearSrgb(reference.IsGray), cms, store, ref refLinearSRgb))
        {
            throw new InvalidOperationException("Could not convert the reference image to linear sRGB.");
        }

        // Build the replacement before releasing the previous reference. A failed conversion or allocation leaves it usable.
        ButteraugliComparator replacement = ButteraugliComparator.Make(configuration, refLinearSRgb.Color!, parameters);
        this.comparator?.Dispose();
        this.comparator = replacement;

        this.width = reference.XSize;
        this.height = reference.YSize;
        this.intensityTarget = reference.Metadata.IntensityTarget;
    }

    /// <summary>
    /// Releases the reference decomposition and its reusable scratch without disposing borrowed source images.
    /// </summary>
    public void Dispose()
    {
        this.comparator?.Dispose();
        this.comparator = null;
    }
}
