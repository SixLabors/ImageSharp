// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Temporary work counters for the comparison with the libaom reference. Not part of the product.
/// </summary>
internal static class Av1WorkCounters
{
    /// <summary>
    /// The counter names, in the order of the libaom counters.
    /// </summary>
    public static readonly string[] Names =
    [
        "pick_partition", "pick_sb_modes", "intra_sby", "luma_mode_yrd", "uniform_tx_yrd", "intra_sbuv", "chroma_uvrd",
        "palette_y_search", "palette_y_rd", "search_tx_type_y", "search_tx_type_uv", "tx_type_iter_y", "tx_type_iter_uv",
        "fwd_xform", "optimize_b", "cost_coeffs", "dist_px_domain", "dist_tx_domain", "recon_intra_inv", "encode_block_intra", "predict_intra",
        "quant", "satd_gate", "subtract", "total",
        "p_select_luma_prediction", "p_refine_luma_mode", "p_luma_model_cost", "p_hog_mask", "p_reference_samples",
        "p_copy_tiled", "p_prune_partitions", "p_chroma_candidate", "p_cfl_estimate", "p_chroma_prediction",
        "p_palette_candidate", "p_chroma_palette", "p_uniform_luma"
    ];

    /// <summary>
    /// The counter values.
    /// </summary>
    public static readonly long[] Values = new long[Names.Length];

    /// <summary>
    /// The accumulated timestamp ticks of each counter.
    /// </summary>
    public static readonly long[] Ticks = new long[Names.Length];

    public const int Quant = 21;
    public const int SatdGate = 22;
    public const int Subtract = 23;
    public const int Total = 24;
    public const int SelectLumaPrediction = 25;
    public const int RefineLumaMode = 26;
    public const int LumaModelCost = 27;
    public const int HogMask = 28;
    public const int ReferenceSamples = 29;
    public const int CopyTiled = 30;
    public const int PrunePartitions = 31;
    public const int ChromaCandidate = 32;
    public const int CflEstimate = 33;
    public const int ChromaPrediction = 34;
    public const int PaletteCandidate = 35;
    public const int ChromaPalette = 36;
    public const int UniformLuma = 37;

    public const int PickPartition = 0;
    public const int PickSbModes = 1;
    public const int IntraSby = 2;
    public const int LumaModeYrd = 3;
    public const int UniformTxYrd = 4;
    public const int IntraSbuv = 5;
    public const int ChromaUvrd = 6;
    public const int PaletteYSearch = 7;
    public const int PaletteYRd = 8;
    public const int SearchTxTypeY = 9;
    public const int SearchTxTypeUv = 10;
    public const int TxTypeIterY = 11;
    public const int TxTypeIterUv = 12;
    public const int FwdXform = 13;
    public const int OptimizeB = 14;
    public const int CostCoeffs = 15;
    public const int DistPxDomain = 16;
    public const int DistTxDomain = 17;
    public const int ReconIntraInv = 18;
    public const int EncodeBlockIntra = 19;
    public const int PredictIntra = 20;

    /// <summary>
    /// Whether the timers read the clock. Off, the timer calls fold away and only the counts remain.
    /// </summary>
    public const bool TimersEnabled = false;

    /// <summary>
    /// Whether the counters count. Off, every count call folds away, so the encoder pays nothing for them.
    /// </summary>
    public const bool CountsEnabled = false;

    /// <summary>
    /// Reads the timer.
    /// </summary>
    /// <returns>The current timestamp.</returns>
    public static long Start() => TimersEnabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

    /// <summary>
    /// Adds the time since a start timestamp to a counter.
    /// </summary>
    /// <param name="index">The counter.</param>
    /// <param name="start">The start timestamp.</param>
#pragma warning disable CS0162 // The timer switch is a compile-time constant.
    public static void Stop(int index, long start)
    {
        if (TimersEnabled)
        {
            Ticks[index] += System.Diagnostics.Stopwatch.GetTimestamp() - start;
        }
    }
#pragma warning restore CS0162

    /// <summary>
    /// Adds one to a counter.
    /// </summary>
    /// <param name="index">The counter.</param>
#pragma warning disable CS0162 // The count switch is a compile-time constant.
    public static void Count(int index)
    {
        if (CountsEnabled)
        {
            Values[index]++;
        }
    }
#pragma warning restore CS0162

    /// <summary>
    /// Clears every counter.
    /// </summary>
    public static void Reset()
    {
        Array.Clear(Values);
        Array.Clear(Ticks);
    }

    /// <summary>
    /// Formats every counter as one line each.
    /// </summary>
    /// <returns>The report.</returns>
    public static string Report()
    {
        System.Text.StringBuilder builder = new();
        builder.Append("WORK ticks_per_ns ").Append(System.Diagnostics.Stopwatch.Frequency / 1e9).Append('\n');
        for (int i = 0; i < Names.Length; i++)
        {
            builder.Append("WORK ").Append(Names[i]).Append(' ').Append(Values[i]).Append(' ').Append(Ticks[i]).Append('\n');
        }

        return builder.ToString();
    }
}
