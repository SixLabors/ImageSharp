// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the parameters used to synthesize AV1 film grain for a decoded frame.
/// </summary>
internal sealed class ObuFilmGrainParameters
{
    /// <summary>
    /// Stores the luma scaling-point coordinates without a per-frame array allocation.
    /// </summary>
    private InlineArray14<byte> pointYValue;

    /// <summary>
    /// Stores the luma scaling-point values without a per-frame array allocation.
    /// </summary>
    private InlineArray14<byte> pointYScaling;

    /// <summary>
    /// Stores the blue-difference scaling-point coordinates without a per-frame array allocation.
    /// </summary>
    private InlineArray10<byte> pointCbValue;

    /// <summary>
    /// Stores the blue-difference scaling-point values without a per-frame array allocation.
    /// </summary>
    private InlineArray10<byte> pointCbScaling;

    /// <summary>
    /// Stores the red-difference scaling-point coordinates without a per-frame array allocation.
    /// </summary>
    private InlineArray10<byte> pointCrValue;

    /// <summary>
    /// Stores the red-difference scaling-point values without a per-frame array allocation.
    /// </summary>
    private InlineArray10<byte> pointCrScaling;

    /// <summary>
    /// Stores the luma autoregressive coefficients without a per-frame array allocation.
    /// </summary>
    private InlineArray24<byte> arCoeffsYPlus128;

    /// <summary>
    /// Stores the blue-difference autoregressive coefficients without a per-frame array allocation.
    /// </summary>
    private InlineArray25<byte> arCoeffsCbPlus128;

    /// <summary>
    /// Stores the red-difference autoregressive coefficients without a per-frame array allocation.
    /// </summary>
    private InlineArray25<byte> arCoeffsCrPlus128;

    /// <summary>
    /// Gets or sets a value indicating whether film grain is applied to the displayed frame.
    /// </summary>
    public bool ApplyGrain { get; set; }

    /// <summary>
    /// Gets or sets the 16-bit seed that initializes pseudo-random film-grain synthesis for this frame.
    /// </summary>
    public uint GrainSeed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this frame signals a complete parameter set instead of inheriting one.
    /// </summary>
    public bool UpdateGrain { get; set; }

    /// <summary>
    /// Gets or sets the physical reference-map index from which this frame inherited its film-grain parameters.
    /// The index must match one of the frame's seven selected inter-reference slots.
    /// </summary>
    public uint FilmGrainParamsRefIdx { get; set; }

    /// <summary>
    /// Gets or sets the number of active luma scaling points in the inclusive range zero through fourteen.
    /// </summary>
    public uint NumYPoints { get; set; }

    /// <summary>
    /// Gets the fourteen-entry storage for the luma scaling-point coordinates.
    /// </summary>
    /// <remarks>
    /// Only the first <see cref="NumYPoints"/> entries are active. Coordinates use the eight-bit scale and must be
    /// strictly increasing; 10-bit and 12-bit sample values are divided by four and sixteen respectively.
    /// </remarks>
    public Span<byte> PointYValue => this.pointYValue;

    /// <summary>
    /// Gets the fourteen-entry storage for the luma scaling-point output values.
    /// </summary>
    /// <remarks>Only the first <see cref="NumYPoints"/> entries are active.</remarks>
    public Span<byte> PointYScaling => this.pointYScaling;

    /// <summary>
    /// Gets or sets a value indicating whether both chroma scaling functions are derived from luma samples.
    /// </summary>
    public bool ChromaScalingFromLuma { get; set; }

    /// <summary>
    /// Gets or sets the number of active blue-difference scaling points in the inclusive range zero through ten.
    /// </summary>
    public uint NumCbPoints { get; set; }

    /// <summary>
    /// Gets or sets the number of active red-difference scaling points in the inclusive range zero through ten.
    /// </summary>
    public uint NumCrPoints { get; set; }

    /// <summary>
    /// Gets the ten-entry storage for the blue-difference scaling-point coordinates.
    /// </summary>
    /// <remarks>Only the first <see cref="NumCbPoints"/> entries are active, and active coordinates must be strictly increasing.</remarks>
    public Span<byte> PointCbValue => this.pointCbValue;

    /// <summary>
    /// Gets the ten-entry storage for the blue-difference scaling-point output values.
    /// </summary>
    /// <remarks>Only the first <see cref="NumCbPoints"/> entries are active.</remarks>
    public Span<byte> PointCbScaling => this.pointCbScaling;

    /// <summary>
    /// Gets the ten-entry storage for the red-difference scaling-point coordinates.
    /// </summary>
    /// <remarks>Only the first <see cref="NumCrPoints"/> entries are active, and active coordinates must be strictly increasing.</remarks>
    public Span<byte> PointCrValue => this.pointCrValue;

    /// <summary>
    /// Gets the ten-entry storage for the red-difference scaling-point output values.
    /// </summary>
    /// <remarks>Only the first <see cref="NumCrPoints"/> entries are active.</remarks>
    public Span<byte> PointCrScaling => this.pointCrScaling;

    /// <summary>
    /// Gets or sets the scaling-function shift minus eight. Values from zero through three select an effective shift
    /// from eight through eleven for every luma and chroma scaling value.
    /// </summary>
    public uint GrainScalingMinus8 { get; set; }

    /// <summary>
    /// Gets or sets the autoregressive neighborhood lag in the inclusive range zero through three.
    /// </summary>
    public uint ArCoeffLag { get; set; }

    /// <summary>
    /// Gets the twenty-four-entry storage for biased luma autoregressive coefficients.
    /// </summary>
    /// <remarks>The active entry count is <c>2 * ArCoeffLag * (ArCoeffLag + 1)</c>.</remarks>
    public Span<byte> ArCoeffsYPlus128 => this.arCoeffsYPlus128;

    /// <summary>
    /// Gets the twenty-five-entry storage for biased blue-difference autoregressive coefficients.
    /// </summary>
    /// <remarks>The active entry count includes one additional luma coefficient when luma scaling points are present.</remarks>
    public Span<byte> ArCoeffsCbPlus128 => this.arCoeffsCbPlus128;

    /// <summary>
    /// Gets the twenty-five-entry storage for biased red-difference autoregressive coefficients.
    /// </summary>
    /// <remarks>The active entry count includes one additional luma coefficient when luma scaling points are present.</remarks>
    public Span<byte> ArCoeffsCrPlus128 => this.arCoeffsCrPlus128;

    /// <summary>
    /// Gets or sets the autoregressive coefficient shift minus six in the inclusive range zero through three.
    /// </summary>
    public uint ArCoeffShiftMinus6 { get; set; }

    /// <summary>
    /// Gets or sets the right shift applied to generated Gaussian grain samples in the inclusive range zero through three.
    /// </summary>
    public uint GrainScaleShift { get; set; }

    /// <summary>
    /// Gets or sets the 8-bit blue-difference sample multiplier used to derive the chroma scaling index.
    /// </summary>
    public uint CbMult { get; set; }

    /// <summary>
    /// Gets or sets the 8-bit average-luma multiplier used to derive the blue-difference scaling index.
    /// </summary>
    public uint CbLumaMult { get; set; }

    /// <summary>
    /// Gets or sets the 9-bit offset used to derive the blue-difference scaling index.
    /// </summary>
    public uint CbOffset { get; set; }

    /// <summary>
    /// Gets or sets the 8-bit red-difference sample multiplier used to derive the chroma scaling index.
    /// </summary>
    public uint CrMult { get; set; }

    /// <summary>
    /// Gets or sets the 8-bit average-luma multiplier used to derive the red-difference scaling index.
    /// </summary>
    public uint CrLumaMult { get; set; }

    /// <summary>
    /// Gets or sets the 9-bit offset used to derive the red-difference scaling index.
    /// </summary>
    public uint CrOffset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether neighboring film-grain blocks are blended across their boundaries.
    /// </summary>
    public bool OverlapFlag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether grained samples are clipped to the restricted range instead of the full range.
    /// </summary>
    public bool ClipToRestrictedRange { get; set; }

    /// <summary>
    /// Replaces the complete film-grain parameter set with values retained by a reference frame.
    /// </summary>
    /// <param name="source">The retained reference-frame parameters.</param>
    public void CopyFrom(ObuFilmGrainParameters source)
    {
        this.ApplyGrain = source.ApplyGrain;
        this.GrainSeed = source.GrainSeed;
        this.UpdateGrain = source.UpdateGrain;
        this.FilmGrainParamsRefIdx = source.FilmGrainParamsRefIdx;
        this.NumYPoints = source.NumYPoints;
        this.pointYValue = source.pointYValue;
        this.pointYScaling = source.pointYScaling;
        this.ChromaScalingFromLuma = source.ChromaScalingFromLuma;
        this.NumCbPoints = source.NumCbPoints;
        this.NumCrPoints = source.NumCrPoints;
        this.pointCbValue = source.pointCbValue;
        this.pointCbScaling = source.pointCbScaling;
        this.pointCrValue = source.pointCrValue;
        this.pointCrScaling = source.pointCrScaling;
        this.GrainScalingMinus8 = source.GrainScalingMinus8;
        this.ArCoeffLag = source.ArCoeffLag;
        this.arCoeffsYPlus128 = source.arCoeffsYPlus128;
        this.arCoeffsCbPlus128 = source.arCoeffsCbPlus128;
        this.arCoeffsCrPlus128 = source.arCoeffsCrPlus128;
        this.ArCoeffShiftMinus6 = source.ArCoeffShiftMinus6;
        this.GrainScaleShift = source.GrainScaleShift;
        this.CbMult = source.CbMult;
        this.CbLumaMult = source.CbLumaMult;
        this.CbOffset = source.CbOffset;
        this.CrMult = source.CrMult;
        this.CrLumaMult = source.CrLumaMult;
        this.CrOffset = source.CrOffset;
        this.OverlapFlag = source.OverlapFlag;
        this.ClipToRestrictedRange = source.ClipToRestrictedRange;
    }

    /// <summary>
    /// Provides inline storage for the maximum luma autoregressive coefficient count.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    [InlineArray(24)]
    private struct InlineArray24<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }

    /// <summary>
    /// Provides inline storage for the ten scaling points permitted on either chroma plane.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    [InlineArray(10)]
    private struct InlineArray10<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }

    /// <summary>
    /// Provides inline storage for the maximum autoregressive coefficient count of either chroma plane.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    [InlineArray(25)]
    private struct InlineArray25<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }
}
