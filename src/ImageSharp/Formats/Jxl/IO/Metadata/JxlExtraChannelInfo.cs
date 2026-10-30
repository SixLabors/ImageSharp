// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

#pragma warning disable SA1401 // Fields should be private

/// <summary>
/// Stores one extra channel's sample representation, dimensions, name and type-specific fields.
/// </summary>
internal sealed class JxlExtraChannelInfo : IJxlFields
{
    private bool allDefault;
    private JxlExtraChannel type;
    private int dimensionShift;
    private string? name;
    private bool alphaAssociated;
    private int cfaChannel;

    /// <summary>
    /// The spot color's three RGB components followed by its solidity. These are codec values, not pixel alpha.
    /// </summary>
    public InlineArray4<float> SpotColor;

    /// <summary>
    /// Initializes an eight-bit, full-resolution, unassociated alpha channel with no name.
    /// </summary>
    public JxlExtraChannelInfo() => JxlBundle.Init(this);

    /// <summary>
    /// Gets or sets whether the wire header uses its one-bit default shortcut.
    /// </summary>
    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    /// <summary>
    /// Gets or sets the extra-channel type.
    /// </summary>
    public JxlExtraChannel Type
    {
        get => this.type;
        set => this.type = value;
    }

    /// <summary>
    /// Gets or sets the sample representation owned by this channel header.
    /// </summary>
    public JxlBitDepthMetadata BitDepth { get; set; } = new();

    /// <summary>
    /// Gets or sets the base-two reduction in channel dimensions relative to the color image.
    /// </summary>
    public int DimensionShift
    {
        get => this.dimensionShift;
        set => this.dimensionShift = value;
    }

    /// <summary>
    /// Gets or sets the channel name. Empty means that no name is present.
    /// </summary>
    public string? Name
    {
        get => this.name;
        set => this.name = value;
    }

    /// <summary>
    /// Gets or sets whether color components are associated with this alpha channel.
    /// </summary>
    public bool AlphaAssociated
    {
        get => this.alphaAssociated;
        set => this.alphaAssociated = value;
    }

    /// <summary>
    /// Gets or sets the channel index in the color-filter array.
    /// </summary>
    public int CfaChannel
    {
        get => this.cfaChannel;
        set => this.cfaChannel = value;
    }

    /// <inheritdoc />
    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            visitor.SetDefault(this);
            return true;
        }

        if (!visitor.Enum(JxlExtraChannel.Alpha, ref this.type))
        {
            return false;
        }

        if (!visitor.VisitNested(this.BitDepth))
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

        if (!visitor.Name(ref this.name))
        {
            return false;
        }

        // Only the selected channel type supplies its conditional fields. The default visitor initializes all three groups.
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
                if (!visitor.F16(0F, ref this.SpotColor[i]))
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
            (>= JxlExtraChannel.Reserved0 and
             <= JxlExtraChannel.Reserved7))
        {
            return false;
        }

        return true;
    }
}
