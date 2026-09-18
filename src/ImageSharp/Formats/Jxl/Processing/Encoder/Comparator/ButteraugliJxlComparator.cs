// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

/// <summary>
/// Compares images using Google Butteraugli.
/// </summary>
internal sealed class ButteraugliJxlComparator(ButteraugliParameters parameters, JxlCmsInterface cms)
    : JxlComparator
{
    private int width;
    private int height;
    private float intensityTarget;
    private ButteraugliComparator? comparator;

    public override float GoodQualityScore => Butteraugli.Butteraugli.ButteraugliFuzzyInverse(1.5f);

    public override float BadQualityScore => Butteraugli.Butteraugli.ButteraugliFuzzyInverse(0.5f);

    public override void CompareWith(Configuration configuration, JxlImageBundle actual, JxlImageF? diffMap, out float score)
    {
        DebugGuard.NotNull(this.comparator, nameof(this.comparator));

        if (this.width != actual.XSize || this.height != actual.YSize)
        {
            throw new InvalidOperationException("Image must have same size");
        }

        JxlImageMetadata metadata = actual.Metadata!;
        JxlImageBundle store = new(metadata);

        JxlTransformsEncoder.TransformIfNeeded(configuration, actual, JxlColorEncoding.LinearSRgb(actual.IsGray), cms, store, out JxlImageBundle actualLinearSRgb);

        JxlImageF tempDiffmap = new(configuration, this.width, this.height);
        JxlImage3F scaledActualLinearSRgb = actualLinearSRgb.Color!;

        if (this.intensityTarget != 0 && actual.Metadata!.IntensityTarget != this.intensityTarget)
        {
            JxlImage3F scaledActualLinearSRgbStore = new(configuration, this.width, this.height);
            scaledActualLinearSRgb = scaledActualLinearSRgbStore;

            float scale = actual.Metadata!.IntensityTarget / this.intensityTarget;
            for (int c = 0; c < 3; ++c)
            {
                for (int y = 0; y < this.height; ++y)
                {
                    Span<float> souceRow = actualLinearSRgb.Color!.PlaneRow(c, y);
                    Span<float> scaledRow = scaledActualLinearSRgb.PlaneRow(c, y);
                    TensorPrimitives.Multiply(souceRow, scale, scaledRow);
                }
            }
        }

        if (!this.comparator.Diffmap(configuration, scaledActualLinearSRgb, tempDiffmap))
        {
            throw new InvalidOperationException("Diffmap failed");
        }

        score = Butteraugli.Butteraugli.ButteraugliScoreFromDiffmap(tempDiffmap, parameters);
        diffMap?.Swap(tempDiffmap);
    }

    public override void SetReferenceImage(Configuration configuration, JxlImageBundle reference)
    {
        JxlImageMetadata metadata = reference.Metadata!;
        JxlImageBundle store = new(metadata);

        JxlTransformsEncoder.TransformIfNeeded(configuration, reference, JxlColorEncoding.LinearSRgb(reference.IsGray), cms, store, out JxlImageBundle refLinearSRgb);
        this.comparator = ButteraugliComparator.Make(configuration, refLinearSRgb.Color!, parameters);

        this.width = reference.XSize;
        this.height = reference.YSize;
        this.intensityTarget = reference.Metadata!.IntensityTarget;
    }
}
