// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

/// <summary>
/// Stores the global-tree flag, weighted-prediction settings and owned transform metadata for one modular group.
/// </summary>
internal sealed class JxlGroupHeader : IJxlFields
{
    private bool useGlobalTree;
    private List<JxlTransform> transforms = [];

    /// <summary>
    /// Initializes the serialized defaults and the owned weighted-prediction header.
    /// </summary>
    public JxlGroupHeader() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets a value indicating whether the group uses the global prediction tree.
    /// </summary>
    public bool UseGlobalTree
    {
        get => this.useGlobalTree;
        set => this.useGlobalTree = value;
    }

    /// <summary>
    /// Gets the owned weighted-prediction settings.
    /// </summary>
    public JxlModularHeader WeightedHeader { get; } = new();

    /// <summary>
    /// Gets the owned transforms, or copies transform metadata from another owner without sharing mutable squeeze steps.
    /// </summary>
    public List<JxlTransform> Transforms
    {
        get => this.transforms;
        set
        {
            List<JxlTransform> replacement = new(value.Count);

            foreach (JxlTransform transform in value)
            {
                replacement.Add(transform.DeepClone());
            }

            // Copy before publishing. The source can be this header's own list, and failure must retain the previous metadata.
            this.transforms = replacement;
        }
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        if (!visitor.Boolean(false, ref this.useGlobalTree))
        {
            return false;
        }

        if (!visitor.VisitNested(this.WeightedHeader))
        {
            return false;
        }

        uint numTransforms = (uint)this.Transforms.Count;

        if (!visitor.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.BitsOffset(4, 2),
            JxlFieldExpressions.BitsOffset(8, 18),
            0,
            ref numTransforms))
        {
            return false;
        }

        // U32 bounds the count to 273. Reading must create actual transform objects, not just list capacity.
        // A default visitor also sets the count to zero, so apply the count for every visitor that changes it.
        int count = (int)numTransforms;

        if (this.transforms.Count > count)
        {
            this.transforms.RemoveRange(count, this.transforms.Count - count);
        }

        this.transforms.EnsureCapacity(count);

        while (this.transforms.Count < count)
        {
            this.transforms.Add(new JxlTransform());
        }

        Span<JxlTransform> sp = CollectionsMarshal.AsSpan(this.transforms);

        for (int i = 0; i < numTransforms; i++)
        {
            if (!visitor.VisitNested(sp[i]))
            {
                return false;
            }
        }

        return true;
    }
}
