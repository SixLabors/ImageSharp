// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Context prediction header
/// </summary>
internal sealed class JxlModularHeader : IJxlFields
{
    // Backing fields for properties so we can get
    // a ref to them.
    private bool allDefault;
    private int p1C;
    private int p2C;
    private int p3Ca;
    private int p3Cb;
    private int p3Cc;
    private int p3Cd;
    private int p3Ce;
    private InlineArray4<uint> w;

    /// <summary>
    /// Gets or sets a value indicating whether all values are default.
    /// </summary>
    public bool AllDefault
    {
        get => this.allDefault;
        set => this.allDefault = value;
    }

    /// <summary>
    /// Gets or sets the p1C coefficient.
    /// </summary>
    public int P1C
    {
        get => this.p1C;
        set => this.p1C = value;
    }

    /// <summary>
    /// Gets or sets the p2C coefficient.
    /// </summary>
    public int P2C
    {
        get => this.p2C;
        set => this.p2C = value;
    }

    /// <summary>
    /// Gets or sets the p3Ca coefficient.
    /// </summary>
    public int P3Ca
    {
        get => this.p3Ca;
        set => this.p3Ca = value;
    }

    /// <summary>
    /// Gets or sets the p3Cb coefficient.
    /// </summary>
    public int P3Cb
    {
        get => this.p3Cb;
        set => this.p3Cb = value;
    }

    /// <summary>
    /// Gets or sets the p3Cc coefficient.
    /// </summary>
    public int P3Cc
    {
        get => this.p3Cc;
        set => this.p3Cc = value;
    }

    /// <summary>
    /// Gets or sets the p3Cd coefficient.
    /// </summary>
    public int P3Cd
    {
        get => this.p3Cd;
        set => this.p3Cd = value;
    }

    /// <summary>
    /// Gets or sets the p3Ce coefficient.
    /// </summary>
    public int P3Ce
    {
        get => this.p3Ce;
        set => this.p3Ce = value;
    }

    /// <summary>
    /// Returns a span to the w array.
    /// </summary>
    /// <returns>Reference to w</returns>
    public Span<uint> GetW() => this.w;

    public bool Visit(JxlVisitor v)
    {
        if (v.AllDefault(this, ref this.allDefault))
        {
            v.SetDefault(this);
            return true;
        }

        if (!VisitP(16, ref this.p1C) ||
            !VisitP(10, ref this.p2C) ||
            !VisitP(7, ref this.p3Ca) ||
            !VisitP(7, ref this.p3Cb) ||
            !VisitP(7, ref this.p3Cc) ||
            !VisitP(0, ref this.p3Cd) ||
            !VisitP(0, ref this.p3Ce) ||
            !v.Bits(4, 0xD, ref this.w[0]) ||
            !v.Bits(4, 0xC, ref this.w[1]) ||
            !v.Bits(4, 0xC, ref this.w[2]) ||
            !v.Bits(4, 0xC, ref this.w[3]))
        {
            return false;
        }

        return true;

        bool VisitP(int value, ref int p)
        {
            ref uint unsignedP = ref Unsafe.As<int, uint>(ref p);
            return v.Bits(5, (uint)value, ref unsignedP);
        }
    }
}
