// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// The film grain a sequence signals: the running parameters, which each shown frame copies before its random seed
/// moves on, and the parameters each reference slot keeps. Reference: cm->film_grain_params, cpi->film_grain_table and
/// the film_grain_params of the reference buffers.
/// </summary>
internal sealed class Av1FilmGrainState
{
    /// <summary>
    /// The running parameters. Reference: cm->film_grain_params.
    /// </summary>
    private readonly ObuFilmGrainParameters parameters = new();

    /// <summary>
    /// The table that gives the parameters of each frame's time, or <see langword="null"/> with a preset. Reference:
    /// cpi->film_grain_table.
    /// </summary>
    private readonly Av1FilmGrainTable? table;

    /// <summary>
    /// The parameters of the frame each reference slot holds. Reference: the film_grain_params of
    /// cm->ref_frame_map.
    /// </summary>
    private readonly ObuFilmGrainParameters[] slotParameters = new ObuFilmGrainParameters[Av1Constants.ReferenceFrameCount];

    /// <summary>
    /// Whether the frame each reference slot holds signals film grain. Reference: the film_grain_params_present of
    /// cm->ref_frame_map.
    /// </summary>
    private readonly bool[] slotPresent = new bool[Av1Constants.ReferenceFrameCount];

    /// <summary>
    /// Whether the current frame signals film grain. Reference: cm->cur_frame->film_grain_params_present.
    /// </summary>
    private bool framePresent;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FilmGrainState"/> class.
    /// </summary>
    /// <param name="preset">The film grain preset from 1 to 16, or 0 to take the parameters from the table.</param>
    /// <param name="table">The film grain table, or <see langword="null"/> with a preset.</param>
    /// <param name="colorConfig">The color configuration of the sequence.</param>
    private Av1FilmGrainState(int preset, Av1FilmGrainTable? table, ObuColorConfig colorConfig)
    {
        this.table = table;
        for (int slot = 0; slot < this.slotParameters.Length; slot++)
        {
            this.slotParameters[slot] = new ObuFilmGrainParameters();
        }

        // A preset applies from the first key frame, a monochrome sequence drops its chroma grain, and full range
        // samples are not clipped. Reference: av1_update_film_grain_parameters() with
        // reset_film_grain_chroma_params().
        if (preset != 0)
        {
            Av1FilmGrainPresets.Load(preset, this.parameters);
            if (colorConfig.IsMonochrome)
            {
                ResetChroma(this.parameters);
            }

            if (colorConfig.ColorRange)
            {
                this.parameters.ClipToRestrictedRange = false;
            }
        }
    }

    /// <summary>
    /// Creates the film grain state of a sequence, or returns <see langword="null"/> when it signals none. A preset
    /// takes priority over a table. Reference: av1_update_film_grain_parameters_seq().
    /// </summary>
    /// <param name="preset">The film grain preset from 1 to 16, or 0 for none.</param>
    /// <param name="table">The film grain table, or <see langword="null"/>.</param>
    /// <param name="colorConfig">The color configuration of the sequence.</param>
    /// <returns>The state, or <see langword="null"/>.</returns>
    public static Av1FilmGrainState? Create(int preset, Av1FilmGrainTable? table, ObuColorConfig colorConfig)
        => preset == 0 && table is null ? null : new Av1FilmGrainState(preset, preset == 0 ? table : null, colorConfig);

    /// <summary>
    /// Sets the film grain of a frame about to be coded. A table gives the parameters of the frame's time. A shown or
    /// showable frame copies the running parameters, an intra frame always signals them in full, and an inter frame
    /// that reuses them names a reference slot that holds them. The running random seed then moves on. Reference:
    /// the aom_film_grain_table_lookup() call of av1_encode_strategy(), the film grain copy before the bitstream is
    /// written, and the reference search of write_film_grain_params().
    /// </summary>
    /// <param name="frameHeader">The frame header.</param>
    /// <param name="timeStamp">The frame's start time in ticks. Reference: *time_stamp.</param>
    public void PrepareFrame(ObuFrameHeader frameHeader, long timeStamp)
    {
        this.framePresent = this.table is null || this.table.Lookup(timeStamp, this.parameters);
        if (!frameHeader.ShowFrame && !frameHeader.ShowableFrame)
        {
            return;
        }

        ObuFilmGrainParameters frameParameters = frameHeader.FilmGrainParameters;
        frameParameters.CopyFrom(this.parameters);
        if (frameHeader.FrameType != ObuFrameType.InterFrame)
        {
            frameParameters.UpdateGrain = true;
        }

        if (frameParameters.ApplyGrain && !frameParameters.UpdateGrain)
        {
            frameParameters.UpdateGrain = true;
            ReadOnlySpan<uint> referenceFrameIndices = frameHeader.GetReferenceFrameIndices();
            for (int reference = 0; reference < Av1Constants.ReferencesPerFrame; reference++)
            {
                int slot = (int)referenceFrameIndices[reference];
                if (this.slotPresent[slot] && AreEquivalent(frameParameters, this.slotParameters[slot]))
                {
                    frameParameters.UpdateGrain = false;
                    frameParameters.FilmGrainParamsRefIdx = (uint)slot;
                    break;
                }
            }
        }

        // Reference: the random_seed step that follows the copy.
        uint seed = (this.parameters.GrainSeed + 3381) & 0xFFFF;
        this.parameters.GrainSeed = seed == 0 ? 7391 : seed;
    }

    /// <summary>
    /// Keeps the film grain of a coded frame in the reference slots it refreshes. Reference: the ref_frame_map update
    /// with cm->cur_frame.
    /// </summary>
    /// <param name="frameHeader">The coded frame header.</param>
    public void RefreshSlots(ObuFrameHeader frameHeader)
    {
        // A frame that is neither shown nor showable keeps the parameters of the frame before it, where libaom keeps
        // whatever its frame buffer last held. Only the reference search of a preset that reuses its grain reads
        // them, and the LAST slot it tries first always holds a shown frame.
        for (int slot = 0; slot < Av1Constants.ReferenceFrameCount; slot++)
        {
            if ((frameHeader.RefreshFrameFlags & (1U << slot)) != 0)
            {
                this.slotParameters[slot].CopyFrom(frameHeader.FilmGrainParameters);
                this.slotPresent[slot] = this.framePresent;
            }
        }
    }

    /// <summary>
    /// Returns whether two parameter sets signal the same grain, without the random seed and the update flag.
    /// Reference: aom_check_grain_params_equiv().
    /// </summary>
    /// <param name="a">The first parameter set.</param>
    /// <param name="b">The second parameter set.</param>
    /// <returns>Whether the sets signal the same grain.</returns>
    private static bool AreEquivalent(ObuFilmGrainParameters a, ObuFilmGrainParameters b)
    {
        // Only the points and coefficients in use are compared, as the reference compares their used length.
        int positions = (int)(2 * a.ArCoeffLag * (a.ArCoeffLag + 1));
        return a.ApplyGrain == b.ApplyGrain &&
            a.NumYPoints == b.NumYPoints &&
            a.PointYValue[..(int)a.NumYPoints].SequenceEqual(b.PointYValue[..(int)a.NumYPoints]) &&
            a.PointYScaling[..(int)a.NumYPoints].SequenceEqual(b.PointYScaling[..(int)a.NumYPoints]) &&
            a.NumCbPoints == b.NumCbPoints &&
            a.PointCbValue[..(int)a.NumCbPoints].SequenceEqual(b.PointCbValue[..(int)a.NumCbPoints]) &&
            a.PointCbScaling[..(int)a.NumCbPoints].SequenceEqual(b.PointCbScaling[..(int)a.NumCbPoints]) &&
            a.NumCrPoints == b.NumCrPoints &&
            a.PointCrValue[..(int)a.NumCrPoints].SequenceEqual(b.PointCrValue[..(int)a.NumCrPoints]) &&
            a.PointCrScaling[..(int)a.NumCrPoints].SequenceEqual(b.PointCrScaling[..(int)a.NumCrPoints]) &&
            a.GrainScalingMinus8 == b.GrainScalingMinus8 &&
            a.ArCoeffLag == b.ArCoeffLag &&
            a.ArCoeffsYPlus128[..positions].SequenceEqual(b.ArCoeffsYPlus128[..positions]) &&
            a.ArCoeffsCbPlus128[..positions].SequenceEqual(b.ArCoeffsCbPlus128[..positions]) &&
            a.ArCoeffsCrPlus128[..positions].SequenceEqual(b.ArCoeffsCrPlus128[..positions]) &&
            a.ArCoeffShiftMinus6 == b.ArCoeffShiftMinus6 &&
            a.CbMult == b.CbMult &&
            a.CbLumaMult == b.CbLumaMult &&
            a.CbOffset == b.CbOffset &&
            a.CrMult == b.CrMult &&
            a.CrLumaMult == b.CrLumaMult &&
            a.CrOffset == b.CrOffset &&
            a.OverlapFlag == b.OverlapFlag &&
            a.ClipToRestrictedRange == b.ClipToRestrictedRange &&
            a.ChromaScalingFromLuma == b.ChromaScalingFromLuma &&
            a.GrainScaleShift == b.GrainScaleShift;
    }

    /// <summary>
    /// Removes the chroma grain. Reference: reset_film_grain_chroma_params().
    /// </summary>
    /// <param name="parameters">The parameters whose chroma grain is removed.</param>
    private static void ResetChroma(ObuFilmGrainParameters parameters)
    {
        parameters.NumCbPoints = 0;
        parameters.NumCrPoints = 0;

        // The offsets keep their values; they are only written with chroma points.
        parameters.CbMult = 0;
        parameters.CbLumaMult = 0;
        parameters.CrMult = 0;
        parameters.CrLumaMult = 0;
        parameters.ChromaScalingFromLuma = false;
        parameters.ArCoeffsCbPlus128.Fill(128);
        parameters.ArCoeffsCrPlus128.Fill(128);
        parameters.PointCbValue.Clear();
        parameters.PointCbScaling.Clear();
        parameters.PointCrValue.Clear();
        parameters.PointCrScaling.Clear();
    }
}
