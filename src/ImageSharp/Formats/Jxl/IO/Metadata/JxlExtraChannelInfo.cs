// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlExtraChannelInfo : IJxlFields
{
    private bool allDefault;
    private JxlExtraChannel type;
    private JxlBitDepthMetadata? bitDepth;
    private int dimensionShift;
    private string? name;
    private bool alphaAssociated;
    private InlineArray4<float> spotColor;
    private int cfaChannel;

    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    public JxlExtraChannel Type
    {
        get => this.type;
        set => this.type = value;
    }

    public JxlBitDepthMetadata? BitDepth
    {
        get => this.bitDepth;
        set => this.bitDepth = value;
    }

    public int DimensionShift
    {
        get => this.dimensionShift;
        set => this.dimensionShift = value;
    }

    public string? Name
    {
        get => this.name;
        set => this.name = value;
    }

    public bool AlphaAssociated
    {
        get => this.alphaAssociated;
        set => this.alphaAssociated = value;
    }

    public InlineArray4<float> SpotColor
    {
        get => this.spotColor;
        set => this.spotColor = value;
    }

    public int CfaChannel
    {
        get => this.cfaChannel;
        set => this.cfaChannel = value;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            // Overwrite all serialized fields, but not any nonserialized_*.
            visitor.SetDefault(this);
            return true;
        }

        // General
        if (!visitor.Enum(JxlExtraChannel.Alpha, ref this.type))
        {
            return false;
        }

        if (!visitor.VisitNested(this.bitDepth!))
        {
            return false;
        }

        if (!visitor.U32(
            JxlFieldExpressions.Value(0u),
            JxlFieldExpressions.Value(3u),
            JxlFieldExpressions.Value(4u),
            JxlFieldExpressions.BitsOffset(3u, 1u),
            0u,
            ref Unsafe.As<int, uint>(ref this.dimensionShift)))
        {
            return false;
        }

        if ((1u << this.dimensionShift) > 8u)
        {
            return false;
        }

        if (!VisitNameString(visitor, ref this.name))
        {
            return false;
        }

        // Conditional
        if (visitor.Conditional(this.type == JxlExtraChannel.Alpha))
        {
            if (!visitor.Boolean(false, ref this.alphaAssociated))
            {
                return false;
            }
        }

        if (visitor.Conditional(this.type == JxlExtraChannel.SpotColor))
        {
            for (int i = 0; i < 4; i++)
            {
                if (!visitor.F16(0F, ref this.spotColor[i]))
                {
                    return false;
                }
            }
        }

        if (visitor.Conditional(this.type == JxlExtraChannel.Cfa))
        {
            if (!visitor.U32(
                JxlFieldExpressions.Value(1u),
                JxlFieldExpressions.Bits(2u),
                JxlFieldExpressions.BitsOffset(4u, 3u),
                JxlFieldExpressions.BitsOffset(8u, 19u),
                1u,
                ref Unsafe.As<int, uint>(ref this.cfaChannel)))
            {
                return false;
            }
        }

        if (this.type is JxlExtraChannel.Unknown or
            >= JxlExtraChannel.Reserved0 and
             <= JxlExtraChannel.Reserved7)
        {
            return false;
        }

        return true;
    }
}
