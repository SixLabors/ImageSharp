// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuFilmGrainParameters
{
    /// <summary>
    /// Gets or sets a value indicating whether film grain should be added to this frame. A value equal to false specifies that film
    /// grain should not be added.
    /// </summary>
    public bool ApplyGrain { get; set; }

    /// <summary>
    /// Gets or sets GrainSeed. This value specifies the starting value for the pseudo-random numbers used during film grain synthesis.
    /// </summary>
    public uint GrainSeed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a new set of parameters should be sent. A value equal to false means that the
    /// previous set of parameters should be used.
    /// </summary>
    public bool UpdateGrain { get; set; }

    /// <summary>
    /// Gets or sets FilmGrainParamsRefIdx. Indicates which reference frame contains the film grain parameters to be used for this frame.
    /// It is a requirement of bitstream conformance that FilmGrainParamsRefIdx is equal to ref_frame_idx[ j ] for some value
    /// of j in the range 0 to REFS_PER_FRAME - 1.
    /// </summary>
    public uint FilmGrainParamsRefidx { get; set; }

    /// <summary>
    /// Gets or sets NumYPoints. Specifies the number of points for the piece-wise linear scaling function of the luma component.
    /// It is a requirement of bitstream conformance that NumYPoints is less than or equal to 14.
    /// </summary>
    public uint NumYPoints { get; set; }

    /// <summary>
    /// Gets or sets PointYValue. Represents the x (luma value) coordinate for the i-th point of the piecewise linear scaling function for
    /// luma component.The values are signaled on the scale of 0..255. (In case of 10 bit video, these values correspond to
    /// luma values divided by 4. In case of 12 bit video, these values correspond to luma values divided by 16.)
    ///
    /// If i is greater than 0, it is a requirement of bitstream conformance that point_y_value[ i ] is greater than point_y_value[ i - 1] (this ensures the x coordinates are specified in increasing order).
    /// </summary>
    public uint[]? PointYValue { get; set; }

    /// <summary>
    /// Gets or sets PointYScaling. Represents the scaling (output) value for the i-th point of the piecewise linear scaling function for luma component.
    /// </summary>
    public uint[]? PointYScaling { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the chroma scaling is inferred from the luma scaling.
    /// </summary>
    public bool ChromaScalingFromLuma { get; set; }

    /// <summary>
    /// Gets or sets NumCbPoints. Specifies the number of points for the piece-wise linear scaling function of the cb component.
    /// It is a requirement of bitstream conformance that NumCbPoints is less than or equal to 10.
    /// </summary>
    public uint NumCbPoints { get; set; }

    /// <summary>
    /// Gets or sets NumCrPoints. Specifies represents the number of points for the piece-wise linear scaling function of the cr component.
    /// It is a requirement of bitstream conformance that NumCrPoints is less than or equal to 10.
    /// </summary>
    public uint NumCrPoints { get; set; }

    /// <summary>
    /// Gets or sets PointCbValue. Represents the x coordinate for the i-th point of the piece-wise linear scaling function for cb
    /// component.The values are signaled on the scale of 0..255.
    /// If i is greater than 0, it is a requirement of bitstream conformance that point_cb_value[ i ] is greater than point_cb_value[ i - 1 ].
    /// </summary>
    public uint[]? PointCbValue { get; set; }

    /// <summary>
    /// Gets or sets PointCbScaling. Represents the scaling (output) value for the i-th point of the piecewise linear scaling function for cb component.
    /// </summary>
    public uint[]? PointCbScaling { get; set; }

    /// <summary>
    /// Gets or sets PointCrValue. Represents the x coordinate for the i-th point of the piece-wise linear scaling function for cr component.
    /// The values are signaled on the scale of 0..255.
    /// If i is greater than 0, it is a requirement of bitstream conformance that point_cr_value[ i ] is greater than point_cr_value[ i - 1 ].
    /// </summary>
    public uint[]? PointCrValue { get; set; }

    /// <summary>
    /// Gets or sets PointCrScaling. Represents the scaling (output) value for the i-th point of the piecewise linear scaling function for cr component.
    /// </summary>
    public uint[]? PointCrScaling { get; set; }

    /// <summary>
    /// Gets or sets GrainScalingMinus8. represents the shift – 8 applied to the values of the chroma component. The
    /// grain_scaling_minus_8 can take values of 0..3 and determines the range and quantization step of the standard deviation of film grain.
    /// </summary>
    public uint GrainScalingMinus8 { get; set; }

    /// <summary>
    /// Gets or sets ArCoeffLag. Specifies the number of auto-regressive coefficients for luma and chroma.
    /// </summary>
    public uint ArCoeffLag { get; set; }

    /// <summary>
    /// Gets or sets ArCoeffsYPlus128. Specifies auto-regressive coefficients used for the Y plane.
    /// </summary>
    public uint[]? ArCoeffsYPlus128 { get; set; }

    /// <summary>
    /// Gets or sets ArCoeffsCbPlus128. Specifies auto-regressive coefficients used for the U plane.
    /// </summary>
    public uint[]? ArCoeffsCbPlus128 { get; set; }

    /// <summary>
    /// Gets or sets ArCoeffsCrPlus128. Specifies auto-regressive coefficients used for the V plane.
    /// </summary>
    public uint[]? ArCoeffsCrPlus128 { get; set; }

    /// <summary>
    /// Gets or sets ArCoeffShiftMinus6. Specifies the range of the auto-regressive coefficients. Values of 0, 1, 2, and 3 correspond to the
    /// ranges for auto-regressive coefficients of[-2, 2), [-1, 1), [-0.5, 0.5) and [-0.25, 0.25) respectively.
    /// </summary>
    public uint ArCoeffShiftMinus6 { get; set; }

    /// <summary>
    /// Gets or sets GrainScaleShift. Specifies how much the Gaussian random numbers should be scaled down during the grain synthesis process.
    /// </summary>
    public uint GrainScaleShift { get; set; }

    /// <summary>
    /// Gets or sets CbMult. Represents a multiplier for the cb component used in derivation of the input index to the cb component scaling function.
    /// </summary>
    public uint CbMult { get; set; }

    /// <summary>
    /// Gets or sets CbLumaMult. Represents a multiplier for the average luma component used in derivation of the input index to the cb component scaling function.
    /// </summary>
    public uint CbLumaMult { get; set; }

    /// <summary>
    /// Gets or sets CbOffset. Represents an offset used in derivation of the input index to the cb component scaling function.
    /// </summary>
    public uint CbOffset { get; set; }

    /// <summary>
    /// Gets or sets CrMult. Represents a multiplier for the cr component used in derivation of the input index to the cr component scaling function.
    /// </summary>
    public uint CrMult { get; set; }

    /// <summary>
    /// Gets or sets CrLumaMult. Represents a multiplier for the average luma component used in derivation of the input index to the cr component scaling function.
    /// </summary>
    public uint CrLumaMult { get; set; }

    /// <summary>
    /// Gets or sets CrOffset. Represents an offset used in derivation of the input index to the cr component scaling function.
    /// </summary>
    public uint CrOffset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the overlap between film grain blocks shall be applied. OverlapFlag equal to false
    /// indicates that the overlap between film grain blocks shall not be applied.
    /// </summary>
    public bool OverlapFlag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether clipping to the restricted (studio) range shall be applied to the sample
    /// values after adding the film grain(see the semantics for color_range for an explanation of studio swing).
    /// ClipToRestrictedRange equal to false indicates that clipping to the full range shall be applied to the sample values after adding the film grain.
    /// </summary>
    public bool ClipToRestrictedRange { get; set; }
}
