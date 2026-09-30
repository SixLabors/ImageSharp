// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

internal sealed class JxlTransform : IJxlFields
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

    public JxlTransformType TransformType { get; set; }

    public int BeginC { get; set; }

    public int RctType { get; set; }

    public int NumC { get; set; }

    public int Colors { get; set; }

    public int Deltas { get; set; }

    public List<JxlSqueezeParameters> Squeezes { get; set; } = [];

    public int MaxDeltaError { get; set; }

    public JxlPredictor Predictor { get; set; }

    public bool OrderedPalette { get; set; } = true;

    public bool LossyPalette { get; set; }

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

    public static void ComputeMinMax(JxlModularChannel channel, out int min, out int max)
    {
        // Start with opposite bounds so the first iteration
        // guarantees to set these values
        min = int.MaxValue;
        max = int.MinValue;

        for (int y = 0; y < channel.Height; y++)
        {
            ReadOnlySpan<int> p = channel.GetRow(y);

            int minRow = TensorPrimitives.Min(p);
            int maxRow = TensorPrimitives.Max(p);

            min = Math.Min(minRow, min);
            max = Math.Max(maxRow, max);
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
