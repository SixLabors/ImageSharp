// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal sealed class JxlGroupHeader : IJxlFields
{
    private bool useGlobalTree;

    public bool UseGlobalTree
    {
        get => this.useGlobalTree;
        set => this.useGlobalTree = value;
    }

    internal JxlModularHeader WeightedHeader { get; set; } = new();

    internal List<JxlTransform> Transforms { get; set; } = [];

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

        _ = visitor.U32(
            JxlFieldExpressions.Value(0),
            JxlFieldExpressions.Value(1),
            JxlFieldExpressions.BitsOffset(4, 2),
            JxlFieldExpressions.BitsOffset(8, 18),
            0,
            ref numTransforms);

        if (visitor.IsReading)
        {
            this.Transforms = new List<JxlTransform>((int)numTransforms);
        }

        Span<JxlTransform> sp = CollectionsMarshal.AsSpan(this.Transforms);

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
