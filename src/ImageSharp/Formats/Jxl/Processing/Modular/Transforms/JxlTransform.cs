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
    /// <summary>
    /// Initializes default parameters and marks the transform invalid until its identifier is read or assigned.
    /// </summary>
    public JxlTransform()
        : this(JxlTransformType.Invalid)
    {
    }

    /// <summary>
    /// Initializes serialized parameter defaults and selects a transform identifier.
    /// </summary>
    /// <param name="id">The transform identifier.</param>
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

    /// <summary>
    /// Gets or sets the encoded transform kind that selects the remaining parameters.
    /// </summary>
    public JxlTransformType TransformType { get; set; }

    /// <summary>
    /// Gets or sets the first selected channel index for an RCT or palette transform.
    /// </summary>
    public int BeginC { get; set; }

    /// <summary>
    /// Gets or sets the reversible color-transform mode, from 0 through 41. Mode 6 selects YCoCg.
    /// </summary>
    public int RctType { get; set; }

    /// <summary>
    /// Gets or sets the number of source channels combined by a palette transform.
    /// </summary>
    public int NumC { get; set; }

    /// <summary>
    /// Gets or sets the number of explicit palette colors, excluding predictive delta entries.
    /// </summary>
    public int Colors { get; set; }

    /// <summary>
    /// Gets or sets the number of predictive delta entries before the palette's color entries.
    /// </summary>
    public int Deltas { get; set; }

    /// <summary>
    /// Gets the owned squeeze steps. An empty parsed list selects image-dependent default steps during metadata application.
    /// </summary>
    public List<JxlSqueezeParameters> Squeezes { get; } = [];

    /// <summary>
    /// Gets or sets the encoder's maximum delta error. This setting is not serialized.
    /// </summary>
    public int MaxDeltaError { get; set; }

    /// <summary>
    /// Gets or sets the prediction method applied to palette delta entries.
    /// </summary>
    public JxlPredictor Predictor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the encoder orders palette colors. This setting is not serialized.
    /// </summary>
    public bool OrderedPalette { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether palette encoding can quantize colors. This setting is not serialized.
    /// </summary>
    public bool LossyPalette { get; set; }

    /// <inheritdoc />
    public JxlTransform DeepClone() => new(this);

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        // Transform identifiers use four exact values, not the broader JPEG XL enum distribution.
        // Keep unsigned wire locals separate from the existing signed channel/count properties.
        uint value = (uint)this.TransformType;

        if (!visitor.U32(
            JxlFieldExpressions.Value((uint)JxlTransformType.Rct),
            JxlFieldExpressions.Value((uint)JxlTransformType.Palette),
            JxlFieldExpressions.Value((uint)JxlTransformType.Squeeze),
            JxlFieldExpressions.Value((uint)JxlTransformType.Invalid),
            (uint)JxlTransformType.Rct,
            ref value)
            || value == (uint)JxlTransformType.Invalid)
        {
            return false;
        }

        this.TransformType = (JxlTransformType)value;

        if (visitor.Conditional(this.TransformType is JxlTransformType.Rct or JxlTransformType.Palette))
        {
            value = (uint)this.BeginC;

            if (!visitor.U32(
                JxlFieldExpressions.Bits(3),
                JxlFieldExpressions.BitsOffset(6, 8),
                JxlFieldExpressions.BitsOffset(10, 72),
                JxlFieldExpressions.BitsOffset(13, 1096),
                0,
                ref value))
            {
                return false;
            }

            this.BeginC = (int)value;
        }

        if (visitor.Conditional(this.TransformType == JxlTransformType.Rct))
        {
            value = (uint)this.RctType;

            if (!visitor.U32(
                JxlFieldExpressions.Value(6),
                JxlFieldExpressions.Bits(2),
                JxlFieldExpressions.BitsOffset(4, 2),
                JxlFieldExpressions.BitsOffset(6, 10),
                6,
                ref value)
                || value >= 42)
            {
                return false;
            }

            this.RctType = (int)value;
        }

        if (visitor.Conditional(this.TransformType == JxlTransformType.Palette))
        {
            value = (uint)this.NumC;

            if (!visitor.U32(
                JxlFieldExpressions.Value(1),
                JxlFieldExpressions.Value(3),
                JxlFieldExpressions.Value(4),
                JxlFieldExpressions.BitsOffset(13, 1),
                3,
                ref value))
            {
                return false;
            }

            this.NumC = (int)value;
            value = (uint)this.Colors;

            if (!visitor.U32(
                JxlFieldExpressions.Bits(8),
                JxlFieldExpressions.BitsOffset(10, 256),
                JxlFieldExpressions.BitsOffset(12, 1280),
                JxlFieldExpressions.BitsOffset(16, 5376),
                256,
                ref value))
            {
                return false;
            }

            this.Colors = (int)value;
            value = (uint)this.Deltas;

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.BitsOffset(8, 1),
                JxlFieldExpressions.BitsOffset(10, 257),
                JxlFieldExpressions.BitsOffset(16, 1281),
                0,
                ref value))
            {
                return false;
            }

            this.Deltas = (int)value;
            value = (uint)this.Predictor;

            if (!visitor.Bits(4, (uint)JxlPredictor.Zero, ref value) || value >= (uint)JxlPredictor.Best)
            {
                return false;
            }

            this.Predictor = (JxlPredictor)value;
        }

        if (visitor.Conditional(this.TransformType == JxlTransformType.Squeeze))
        {
            value = (uint)this.Squeezes.Count;

            if (!visitor.U32(
                JxlFieldExpressions.Value(0),
                JxlFieldExpressions.BitsOffset(4, 1),
                JxlFieldExpressions.BitsOffset(6, 9),
                JxlFieldExpressions.BitsOffset(8, 41),
                0,
                ref value))
            {
                return false;
            }

            // The decoded count is at most 296. A default visitor also clears an earlier explicit sequence to select the default squeeze.
            int count = (int)value;

            if (this.Squeezes.Count > count)
            {
                this.Squeezes.RemoveRange(count, this.Squeezes.Count - count);
            }

            this.Squeezes.EnsureCapacity(count);

            while (this.Squeezes.Count < count)
            {
                this.Squeezes.Add(new JxlSqueezeParameters());
            }

            // Squeeze steps have only primitive fields, with no all-default flag or extensions. Visit the actual value entries
            // directly so decoded fields persist without boxing a struct through IJxlFields. The current bundle owns their bit budget.
            Span<JxlSqueezeParameters> squeezes = CollectionsMarshal.AsSpan(this.Squeezes);

            for (int i = 0; i < squeezes.Length; i++)
            {
                if (!squeezes[i].Visit(visitor))
                {
                    return false;
                }
            }
        }

        return true;
    }

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
