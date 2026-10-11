// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlToneMapping : IJxlFields
{
    private bool allDefault;
    private float intensityTarget;
    private float lowerBoundIntensityLevel;
    private bool relativeToMaxDisplay;
    private float linearBelow;
    private float minNits;

    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    public float IntensityTarget
    {
        get => this.intensityTarget;
        set => this.intensityTarget = value;
    }

    public float LowerBoundIntensityLevel
    {
        get => this.lowerBoundIntensityLevel;
        set => this.lowerBoundIntensityLevel = value;
    }

    public bool RelativeToMaxDisplay
    {
        get => this.relativeToMaxDisplay;
        set => this.relativeToMaxDisplay = value;
    }

    public float LinearBelow
    {
        get => this.linearBelow;
        set => this.linearBelow = value;
    }

    public float MinimumNits
    {
        get => this.minNits;
        set => this.minNits = value;
    }

    public bool Visit(JxlVisitor visitor)
    {
        if (visitor.AllDefault(this, ref this.allDefault))
        {
            // Overwrite all serialized fields, but not any nonserialized_*.
            visitor.SetDefault(this);
            return true;
        }

        if (!visitor.F16(JxlFieldExpressions.Value(JxlConstants.DefaultIntensityTarget), ref this.intensityTarget))
        {
            return false;
        }

        if (this.intensityTarget <= 0F)
        {
            return false;
        }

        if (!visitor.F16(0F, ref this.minNits))
        {
            return false;
        }

        if (this.minNits < 0F || this.minNits > this.intensityTarget)
        {
            return false;
        }

        if (!visitor.Boolean(false, ref this.relativeToMaxDisplay))
        {
            return false;
        }

        if (!visitor.F16(0F, ref this.linearBelow))
        {
            return false;
        }

        if (this.linearBelow < 0F || (this.relativeToMaxDisplay && this.linearBelow > 1F))
        {
            return false;
        }

        return true;
    }
}
