// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Stores one modular transform and owns its mutable squeeze steps.
/// </summary>
internal sealed class JxlTransform : IJxlFields, IDeepCloneable<JxlTransform>
{
    public JxlTransform()
        : this(JxlTransformType.Invalid)
    {
    }

    public JxlTransform(JxlTransformType id)
    {
        JxlBundle.Init(this);
        this.TransformType = id;
    }

    /// <summary>
    /// Copies transform metadata without sharing the list that default squeeze expansion can change.
    /// </summary>
    /// <param name="source">The parsed transform to copy.</param>
    private JxlTransform(JxlTransform source)
    {
        this.TransformType = source.TransformType;
        this.BeginC = source.BeginC;
        this.RctType = source.RctType;
        this.NumC = source.NumC;
        this.Colors = source.Colors;
        this.Deltas = source.Deltas;
        this.MaxDeltaError = source.MaxDeltaError;
        this.Predictor = source.Predictor;
        this.OrderedPalette = source.OrderedPalette;
        this.LossyPalette = source.LossyPalette;

        // Squeeze steps are value types. Copying them into this owned list separates all mutable transform metadata.
        this.Squeezes.AddRange(source.Squeezes);
    }

    public JxlTransformType TransformType { get; set; }

    public int BeginC { get; set; }

    public int RctType { get; set; }

    public int NumC { get; set; }

    public int Colors { get; set; }

    public int Deltas { get; set; }

    /// <summary>
    /// Gets the owned squeeze steps. An empty parsed list selects image-dependent default steps during metadata application.
    /// </summary>
    public List<JxlSqueezeParameters> Squeezes { get; } = [];

    public int MaxDeltaError { get; set; }

    public JxlPredictor Predictor { get; set; }

    public bool OrderedPalette { get; set; } = true;

    public bool LossyPalette { get; set; }

    /// <inheritdoc />
    public JxlTransform DeepClone() => new(this);

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();

    public static void CheckEqualChannels(JxlModularImage image, int c1, int c2)
    {
        int channelsCount = image.Channels.Count;

        if (c1 > channelsCount || c2 >= channelsCount || c2 < c1)
        {
            throw new InvalidOperationException($"Invalid channel range: {c1}..{c2} (there are only {channelsCount} channels)");
        }

        if (c1 < image.MetaChannels && c2 >= image.MetaChannels)
        {
            throw new InvalidOperationException("Invalid: transforming mix of meta and nonmeta");
        }

        JxlModularChannel ch1 = image.Channels[c1];
        for (int c = c1 + 1; c <= c2; c++)
        {
            JxlModularChannel ch2 = image.Channels[c];
            if (ch1.Width != ch2.Width ||
                ch1.Height != ch2.Height ||
                ch1.HorizontalShift != ch2.HorizontalShift ||
                ch1.VerticalShift != ch2.VerticalShift)
            {
                throw new InvalidOperationException($"Channel {c} is not equal");
            }
        }
    }

    public void Inverse(Configuration configuration, JxlModularImage input, JxlModularHeader wpHeader)
    {
        switch (this.TransformType)
        {
            case JxlTransformType.Rct:
                JxlRct.InverseRct(configuration, input, this.BeginC, this.RctType);
                break;

            case JxlTransformType.Squeeze:
                JxlSqueeze.InverseSqueeze(configuration, input, CollectionsMarshal.AsSpan(this.Squeezes));
                break;

            case JxlTransformType.Palette:
                JxlPalette.InversePalette(configuration, input, this.BeginC, this.Colors, this.Deltas, this.Predictor, wpHeader);
                break;

            default:
                throw new InvalidOperationException($"Unknown transform: {this.TransformType}");
        }
    }

    public void MetaApply(Configuration configuration, JxlModularImage image)
    {
        switch (this.TransformType)
        {
            case JxlTransformType.Rct:
                CheckEqualChannels(image, this.BeginC, this.BeginC + 2);
                break;

            case JxlTransformType.Squeeze:
                JxlSqueeze.MetaSqueeze(configuration, image, this.Squeezes);
                break;

            case JxlTransformType.Palette:
                JxlPalette.MetaPalette(configuration, image, this.BeginC, this.BeginC + this.NumC - 1, this.Colors, this.Deltas);
                break;

            default:
                throw new InvalidOperationException($"Unknown transform: {this.TransformType}");
        }
    }
}
