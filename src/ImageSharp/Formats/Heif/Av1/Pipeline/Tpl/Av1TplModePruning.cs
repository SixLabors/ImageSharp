// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The pruning of the coding mode search that reads the temporal dependency statistics: inter modes whose reference
/// predicted much worse than the best reference in the model, and the intra search of inter frames through a small
/// neural network over the model's intra and inter costs.
/// </summary>
internal static class Av1TplModePruning
{
    /// <summary>
    /// The number of features of the intra pruning network. Reference: NUM_FEATURES_12.
    /// </summary>
    private const int FeatureCount = 6;

    /// <summary>
    /// The number of hidden nodes of the intra pruning network. Reference: NUM_LAYER_0_UNITS_12.
    /// </summary>
    private const int HiddenCount = 24;

    /// <summary>
    /// The largest number of dynamic reference vectors searched. Reference: MAX_REF_MV_SEARCH.
    /// </summary>
    private const int MaximumReferenceVectorSearch = 3;

    /// <summary>
    /// Gets the pruning thresholds in quarters by level and reference vector index, the last column for global motion.
    /// Reference: tpl_inter_mode_prune_mul_factor.
    /// </summary>
    private static ReadOnlySpan<byte> InterModePruneFactors => [6, 6, 6, 4, 6, 4, 4, 4, 5, 4, 4, 4];

    /// <summary>
    /// Gets the hidden layer weights of the intra pruning network for frames up to 480 lines, one row of six weights
    /// per node. Reference: av1_intrap_hiddenlayer_0_kernel_12.
    /// </summary>
    private static ReadOnlySpan<float> IntraHiddenWeights =>
    [
        7.28372f, -1.3333898f, -1.3180022f, -0.007156151f, -0.40799126f,
        -0.57538104f, -31.81647f, 6.7057495f, 6.351472f, -0.029544508f,
        0.026801195f, 1.12863f, -0.70769817f, -0.24183524f, 0.0649113f,
        -0.7189517f, 0.21791299f, 0.12840256f, -0.56424767f, 0.16924907f,
        0.4605501f, -0.170895f, -0.60358995f, -0.15383226f, -4.0523643f,
        0.6961917f, 1.3100256f, -0.4189354f, 0.37264112f, -0.14555685f,
        10.628014f, 8.184437f, 8.941916f, -0.011731001f, -0.45127156f,
        0.42704004f, 36.84277f, 8.988796f, 8.844238f, 0.00030091056f,
        -0.022038324f, 1.3566176f, -8.863219f, -0.84811693f, -1.0908632f,
        0.00023130262f, -1.0698471f, -6.755927f, 7.1711984f, 4.7216063f,
        3.5099216f, -0.6650184f, 0.5935173f, -0.6696286f, 11.8595295f,
        0.3001874f, 0.29822728f, 0.04319222f, -1.203178f, 1.1210147f,
        0.035045594f, -0.20559944f, -0.015388541f, -0.7857941f, -0.94100875f,
        -0.1278549f, -19.22603f, 7.9466896f, 6.5048656f, -0.22195444f,
        0.19061874f, 1.3927288f, -8.896529f, -0.48146892f, -1.6098932f,
        -0.0030235797f, -0.6533787f, -2.1333003f, -22.256454f, -4.934058f,
        -4.4707212f, -0.015831878f, -0.4243649f, -2.776269f, -0.23762038f,
        0.1820098f, -0.51865315f, -1.1893421f, 0.34969202f, 0.10636194f,
        14.545696f, 1.3849198f, 2.6815193f, -0.5145498f, 0.45948258f,
        -0.8842355f, -0.9111363f, -0.39652422f, 0.077266276f, -0.68084997f,
        0.4593515f, -0.28872707f, -6.936231f, 1.12253f, 1.7616503f,
        -0.014069137f, -0.0052156276f, -4.5095444f, 6.2076726f, -0.058755957f,
        -0.4675936f, -0.13039507f, 0.12094394f, -0.07285393f, 68.26125f,
        7.4893136f, 8.770954f, 0.020274093f, -0.027877754f, 1.6579602f,
        -0.1825479f, 0.34832543f, 0.07472531f, -0.44812247f, -1.0941806f,
        -0.16749863f, 1.1394324f, 0.47983396f, -0.99983627f, -0.00064249727f,
        -1.3345739f, -0.057157427f, -18.14875f, 16.506035f, 15.539248f,
        0.013191509f, -0.021674965f, -25.006235f, 0.51220596f, 0.7334426f,
        0.81836903f, -1.0443225f, 0.4459505f, -1.2045046f
    ];

    /// <summary>
    /// Gets the hidden layer biases of the intra pruning network for frames up to 480 lines.
    /// Reference: av1_intrap_hiddenlayer_0_bias_12.
    /// </summary>
    private static ReadOnlySpan<float> IntraHiddenBiases =>
    [
        -4.154915f, 14.33833f, 0.0f, 0.0f, 2.0440118f, 12.40922f,
        -16.77514f, 0.5879813f, 3.2305415f, 0.8303539f, 0.0f, 14.488708f,
        2.94393f, 1.874383f, 0.0f, -0.53140444f, 0.0f, 1.8456234f,
        -0.55427986f, -19.856262f, 0.0f, 0.17281002f, 48.31631f, 0.0f
    ];

    /// <summary>
    /// Gets the output weights of the intra pruning network for frames up to 480 lines, one row of 24 weights per
    /// output. Reference: av1_intrap_logits_kernel_12.
    /// </summary>
    private static ReadOnlySpan<float> IntraOutputWeights =>
    [
        0.26843873f, -0.09576241f, 0.34427166f, 0.09914787f, -0.10275399f,
        0.02999484f, -0.1467772f, 0.11594324f, 0.29200763f, 0.0067976206f,
        0.050393578f, -0.018694371f, 0.3333476f, 0.2127221f, 0.35128218f,
        0.19968672f, 0.08099991f, 0.084850654f, -0.16045967f, 0.30286232f,
        0.6164765f, -0.27140254f, 0.08210814f, 0.34852806f, 0.25028184f,
        -0.12188078f, 0.16310331f, 0.31253803f, -0.10792341f, 0.065858394f,
        -0.1349708f, 0.08948815f, 0.31905392f, 0.03680656f, -0.05040944f,
        -0.051539157f, 0.3211852f, 0.2137136f, 0.45037416f, 0.22748767f,
        -0.10978614f, 0.06475646f, -0.16954158f, 0.32831904f, 0.16479677f,
        -0.30020145f, 0.066221856f, 0.37213042f
    ];

    /// <summary>
    /// Gets the output biases of the intra pruning network for frames up to 480 lines.
    /// Reference: av1_intrap_logits_bias_12.
    /// </summary>
    private static ReadOnlySpan<float> IntraOutputBiases => [0.95783f, -0.95823103f];

    /// <summary>
    /// Gets the hidden layer weights of the intra pruning network for larger frames.
    /// Reference: av1_intraph_hiddenlayer_0_kernel_15.
    /// </summary>
    private static ReadOnlySpan<float> HighDefinitionHiddenWeights =>
    [
        -0.77480125f, 0.3219551f, -0.015702145f, -0.5310235f, 0.5254026f,
        -1.1522819f, 2.682016f, 0.08001052f, -0.2539285f, 0.04711023f,
        -0.81296307f, 0.2675382f, 0.1952474f, -0.0664705f, 1.2989824f,
        -0.3150117f, -0.8022715f, 0.045423955f, -27.584324f, -2.5608704f,
        -3.2280366f, 0.05272543f, -0.47141576f, -0.07644298f, -53.77942f,
        -22.393923f, -23.027853f, -0.00015186476f, -0.010696465f, 2.7064638f,
        -22.776028f, 11.514891f, 11.138167f, -0.001243723f, -0.4802433f,
        -8.758646f, 0.26398206f, -0.23485385f, 0.27586034f, -0.004954741f,
        -0.4935232f, -0.017607696f, 69.56049f, -1.1756641f, -0.052366666f,
        -0.38052833f, 0.32474658f, 0.04634263f, 0.8583235f, -0.528438f,
        -0.7868907f, -0.4757781f, 0.4620985f, -0.70621157f, 231.40195f,
        6.805205f, 9.420295f, 0.02585775f, -0.03480937f, 1.3577378f,
        0.1758226f, 15.056758f, 14.437874f, -0.1305005f, 0.115103304f,
        0.21297209f, 55.821743f, -6.611156f, -6.8552365f, -0.011928095f,
        -0.2042175f, 1.2557873f, -1.0722278f, -0.2683614f, 0.48318478f,
        -0.73739994f, 0.54055226f, -0.03224738f, -0.06767959f, -0.21015017f,
        0.29171246f, -0.6937296f, -1.2342545f, -0.41278538f, -37.9365f,
        17.68424f, 16.263042f, -0.074828684f, 0.06607806f, -0.16763286f,
        13.594707f, 0.6152676f, -0.4371223f, -0.8365592f, 0.8273623f,
        -1.2126317f, 0.1216157f, -1.3002136f, -0.18856938f, -0.2589358f,
        -0.76897144f, 0.21777137f, -122.25033f, -0.23490006f, -3.1238277f,
        -0.13916978f, 0.08576391f, -1.7391548f, -116.24812f, 14.906071f,
        13.468357f, 0.02332889f, -0.034617376f, -18.506111f, 0.7500542f,
        -1.1882535f, 0.40848416f, -0.28434393f, -0.71471655f, -0.29188696f,
        -0.46588746f, -0.17324813f, -0.62460244f, -1.1801276f, 0.28993344f,
        -0.22072886f, 129.2688f, -0.33782578f, -0.34836572f, -0.034112718f,
        -0.023666814f, -0.5865087f, -33.484146f, 1.1431375f, 0.56056374f,
        -0.0049730353f, -0.24347587f, -1.3003352f, 0.88973033f, 0.8499571f,
        -0.5678484f, -0.39009875f, -0.062105156f, -0.13965102f
    ];

    /// <summary>
    /// Gets the hidden layer biases of the intra pruning network for larger frames.
    /// Reference: av1_intraph_hiddenlayer_0_bias_15.
    /// </summary>
    private static ReadOnlySpan<float> HighDefinitionHiddenBiases =>
    [
        0.0f, -0.2926711f, 0.0f, -1.0303509f, -27.459345f, 12.412848f,
        0.0f, -2.5971522f, -0.02733541f, -19.881912f, 14.391992f, -8.249469f,
        0.0f, 0.0f, 13.676118f, -0.6472994f, -0.07189449f, 1.1986839f,
        52.479107f, 0.0f, 0.0f, -3.0187025f, 1.4435643f, 0.0f
    ];

    /// <summary>
    /// Gets the output weights of the intra pruning network for larger frames.
    /// Reference: av1_intraph_logits_kernel_15.
    /// </summary>
    private static ReadOnlySpan<float> HighDefinitionOutputWeights =>
    [
        0.05390722f, -0.06859513f, 0.036842898f, 0.190772f, 0.13623567f,
        0.09321194f, 0.2314745f, -0.13958375f, -0.3058229f, -0.0104543045f,
        0.11336068f, -0.276115f, 0.00470723f, -0.49123898f, -0.15988174f,
        0.087681435f, 0.022517204f, 0.073877744f, 0.2968856f, -0.1401399f,
        -0.38788354f, -0.26005393f, -0.39564916f, -0.16195515f, 0.2680102f,
        -0.032179773f, -0.35758728f, 0.25819537f, 0.11468631f, 0.13573235f,
        -0.2672175f, 0.016490124f, 0.048118807f, 0.020319486f, 0.07892215f,
        -0.21821865f, 0.08434734f, 0.3129456f, -0.18215221f, 0.08884877f,
        -0.35621428f, 0.11405768f, 0.27370325f, 0.14956686f, 0.01604587f,
        -0.18334487f, -0.42385718f, -0.08033409f
    ];

    /// <summary>
    /// Gets the output biases of the intra pruning network for larger frames.
    /// Reference: av1_intraph_logits_bias_15.
    /// </summary>
    private static ReadOnlySpan<float> HighDefinitionOutputBiases => [0.83619016f, -0.8340626f];

    /// <summary>
    /// Sums the model prediction error of each reference over the 16x16 blocks of a coding block, and returns the
    /// smallest nonzero sum among the references the selective pruning keeps, or the maximum value.
    /// Reference: get_block_level_tpl_stats().
    /// </summary>
    /// <param name="statisticsReady">Whether the statistics of the frame are ready.</param>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="modeInfoRow">The block row in mode-information units.</param>
    /// <param name="modeInfoColumn">The block column in mode-information units.</param>
    /// <param name="validReferences">
    /// For each reference LAST to ALTREF, whether the model keeps it or the selective pruning does not drop it.
    /// </param>
    /// <param name="referenceInterCosts">Receives the summed prediction error of each reference. Reference: ref_inter_cost.</param>
    /// <returns>The best inter cost. Reference: best_inter_cost.</returns>
    public static long GetBlockLevelStatistics(
        bool statisticsReady,
        Av1TplFrameStatistics frame,
        Av1BlockSize blockSize,
        int modeInfoRow,
        int modeInfoColumn,
        ReadOnlySpan<bool> validReferences,
        Span<long> referenceInterCosts)
    {
        referenceInterCosts.Clear();

        // The caller zeroes the pruning information, which keeps the best cost zero without ready statistics.
        if (!statisticsReady)
        {
            return 0;
        }

        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        int rowEnd = Math.Min(modeInfoRow + blockSize.Get4x4HighCount(), frame.ModeInfoRows);
        int columnEnd = Math.Min(modeInfoColumn + blockSize.Get4x4WideCount(), frame.ModeInfoColumns);
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = modeInfoRow; row < rowEnd; row += Step)
        {
            for (int column = modeInfoColumn; column < columnEnd; column += Step)
            {
                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
                {
                    referenceInterCosts[reference] += block.PredictionError[reference];
                }
            }
        }

        // Unsearched references sum to zero and are not candidates.
        long bestInterCost = long.MaxValue;
        for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
        {
            long cost = referenceInterCosts[reference];
            if (cost != 0 && cost < bestInterCost && validReferences[reference])
            {
                bestInterCost = cost;
            }
        }

        return bestInterCost;
    }

    /// <summary>
    /// Returns whether an inter mode is skipped because its reference predicted much worse than the best reference in
    /// the model. Level one prunes only LAST2 pairs, level two spares new vector modes, and higher levels compare against
    /// thresholds by dynamic reference index. Reference: prune_modes_based_on_tpl_stats().
    /// </summary>
    /// <param name="referenceInterCosts">The summed prediction error of each reference.</param>
    /// <param name="bestInterCost">The best inter cost.</param>
    /// <param name="first">The first reference type, LAST to ALTREF.</param>
    /// <param name="second">The second reference type, or zero for a single reference.</param>
    /// <param name="referenceVectorIndex">The dynamic reference vector index.</param>
    /// <param name="mode">The inter mode.</param>
    /// <param name="level">The pruning level. Reference: prune_inter_modes_based_on_tpl.</param>
    /// <returns><see langword="true"/> when the mode is skipped.</returns>
    public static bool PruneInterMode(
        ReadOnlySpan<long> referenceInterCosts,
        long bestInterCost,
        int first,
        int second,
        int referenceVectorIndex,
        Av1PredictionMode mode,
        int level)
    {
        const int Last2 = 2;
        bool usesLast2 = first == Last2 || second == Last2;
        if (level == 1 && !usesLast2)
        {
            return false;
        }

        bool hasNewVector = mode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector or
            Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NewNearestMotionVector or
            Av1PredictionMode.NearNewMotionVector or Av1PredictionMode.NewNearMotionVector;

        if (level == 2 && hasNewVector)
        {
            return false;
        }

        if (bestInterCost == long.MaxValue)
        {
            return false;
        }

        // A compound pair is as bad as its worse reference.
        long currentInterCost = second > 0
            ? Math.Max(referenceInterCosts[first - 1], referenceInterCosts[second - 1])
            : referenceInterCosts[first - 1];

        if (usesLast2)
        {
            return currentInterCost > bestInterCost;
        }

        bool global = mode is Av1PredictionMode.GlobalMotionVector or Av1PredictionMode.GlobalGlobalMotionVector;
        int pruneIndex = global ? MaximumReferenceVectorSearch : referenceVectorIndex;
        int pruneLevel = level - 2;
        int factor = InterModePruneFactors[(pruneLevel * (MaximumReferenceVectorSearch + 1)) + pruneIndex];
        return currentInterCost > ((factor * bestInterCost) >> 2);
    }

    /// <summary>
    /// Returns the mean model inter and intra costs over the 16x16 blocks of a coding block, available only for
    /// superblocks fully inside the frame; otherwise both stay at -1. Reference: calculate_cost_from_tpl_data().
    /// </summary>
    /// <param name="superblockInterCosts">The superblock inter costs of <see cref="Av1TplDecisions.GetSuperblockStatistics"/>.</param>
    /// <param name="superblockIntraCosts">The superblock intra costs.</param>
    /// <param name="blockCount">The number of superblock blocks inside the frame. Reference: tpl_data_count.</param>
    /// <param name="stride">The number of blocks per superblock row. Reference: tpl_stride.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="modeInfoRow">The block row in mode-information units.</param>
    /// <param name="modeInfoColumn">The block column in mode-information units.</param>
    /// <param name="interCost">Receives the mean inter cost, or -1.</param>
    /// <param name="intraCost">Receives the mean intra cost, or -1.</param>
    public static void GetCostFromTplData(
        ReadOnlySpan<long> superblockInterCosts,
        ReadOnlySpan<long> superblockIntraCosts,
        int blockCount,
        int stride,
        int superblockModeInfoSize,
        Av1BlockSize blockSize,
        int modeInfoRow,
        int modeInfoColumn,
        out long interCost,
        out long intraCost)
    {
        interCost = -1;
        intraCost = -1;
        const int ModelModeInfo = Av1TplModelConstants.BlockSize >> 2;
        int length = (superblockModeInfoSize / ModelModeInfo) * (superblockModeInfoSize / ModelModeInfo);
        if (blockCount != length)
        {
            return;
        }

        int wide = blockSize.Get4x4WideCount() / ModelModeInfo;
        int high = blockSize.Get4x4HighCount() / ModelModeInfo;
        if (wide < 1 || high < 1)
        {
            return;
        }

        int offsetRow = modeInfoRow % superblockModeInfoSize;
        int offsetColumn = modeInfoColumn % superblockModeInfoSize;
        int start = ((offsetRow / ModelModeInfo) * stride) + (offsetColumn / ModelModeInfo);

        // The reference encoder accumulates onto its -1 initial values before dividing.
        for (int k = 0; k < high; k++)
        {
            for (int l = 0; l < wide; l++)
            {
                interCost += superblockInterCosts[start + (k * stride) + l];
                intraCost += superblockIntraCosts[start + (k * stride) + l];
            }
        }

        interCost /= wide * high;
        intraCost /= wide * high;
    }

    /// <summary>
    /// Returns whether the network skips the intra search of an inter frame block, from the transform skip of the best
    /// inter mode, the block dimensions, the model intra and inter costs, and the quantizer. The network runs only when
    /// both costs are available. Reference: the neural network branch of skip_intra_modes_in_interframe().
    /// </summary>
    /// <param name="bestModeSkipsTransform">Whether the best inter mode skips its transform. Reference: best_mbmode.skip_txfm.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="intraCost">The mean model intra cost.</param>
    /// <param name="interCost">The mean model inter cost.</param>
    /// <param name="qIndex">The block quantizer. Reference: x->qindex.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="minimumFrameDimension">The shorter frame dimension.</param>
    /// <returns><see langword="true"/> when the intra search is skipped.</returns>
    public static bool SkipIntraByNetwork(
        bool bestModeSkipsTransform,
        Av1BlockSize blockSize,
        long intraCost,
        long interCost,
        int qIndex,
        Av1BitDepth bitDepth,
        int minimumFrameDimension)
    {
        if (interCost < 0 || intraCost < 0)
        {
            return false;
        }

        Span<float> features = stackalloc float[FeatureCount];
        features[0] = bestModeSkipsTransform ? 1f : 0f;
        features[1] = blockSize.Get4x4WidthLog2();
        features[2] = blockSize.Get4x4HeightLog2();
        features[3] = intraCost;
        features[4] = interCost;
        int acQuantizer = Av1QuantizationLookup.GetAcQuant(qIndex, 0, bitDepth);
        int maximumAcQuantizer = Av1QuantizationLookup.GetAcQuant(Av1Constants.MaxQ, 0, bitDepth);
        features[5] = maximumAcQuantizer / acQuantizer;

        Span<float> scores = stackalloc float[2];
        bool smallFrame = minimumFrameDimension <= 480;
        Predict(
            features,
            smallFrame ? IntraHiddenWeights : HighDefinitionHiddenWeights,
            smallFrame ? IntraHiddenBiases : HighDefinitionHiddenBiases,
            smallFrame ? IntraOutputWeights : HighDefinitionOutputWeights,
            smallFrame ? IntraOutputBiases : HighDefinitionOutputBiases,
            scores);

        // For two outputs the softmax maximum is 1 / (1 + e^-|diff|), so the scores are compared directly. Every
        // pruning level uses the same threshold.
        const float Threshold = 1.4f;
        return scores[1] > scores[0] + Threshold;
    }

    /// <summary>
    /// Evaluates the one hidden layer network with the float arithmetic order of the x64 reference build: the six
    /// inputs of the hidden layer accumulate one by one onto the bias, and the 24 hidden nodes of each output accumulate
    /// in eight lanes whose sums add pairwise. Reference: av1_nn_predict_avx2() with its scalar fallback for inputs that
    /// are not a multiple of four and nn_propagate_8to1() for the output layer.
    /// </summary>
    private static void Predict(
        ReadOnlySpan<float> features,
        ReadOnlySpan<float> hiddenWeights,
        ReadOnlySpan<float> hiddenBiases,
        ReadOnlySpan<float> outputWeights,
        ReadOnlySpan<float> outputBiases,
        Span<float> scores)
    {
        Span<float> hidden = stackalloc float[HiddenCount];
        for (int node = 0; node < HiddenCount; node++)
        {
            float value = hiddenBiases[node];
            for (int input = 0; input < FeatureCount; input++)
            {
                value += features[input] * hiddenWeights[(node * FeatureCount) + input];
            }

            hidden[node] = Math.Max(value, 0f);
        }

        Span<float> lanes = stackalloc float[8];
        for (int output = 0; output < scores.Length; output++)
        {
            // Lane j holds inputs j, j + 8 and j + 16, each product added in turn.
            lanes.Clear();
            for (int chunk = 0; chunk < HiddenCount; chunk += 8)
            {
                for (int lane = 0; lane < 8; lane++)
                {
                    lanes[lane] += hidden[chunk + lane] * outputWeights[(output * HiddenCount) + chunk + lane];
                }
            }

            // The high half adds onto the low half, then adjacent pairs add, then the two pair sums.
            float sum0 = lanes[0] + lanes[4];
            float sum1 = lanes[1] + lanes[5];
            float sum2 = lanes[2] + lanes[6];
            float sum3 = lanes[3] + lanes[7];
            float total = (sum2 + sum3) + (sum0 + sum1);
            scores[output] = outputBiases[output] + total;
        }

        // The outputs are rounded to nine fractional bits: the float product widens to double before the half is
        // added, and the truncated integer multiplies the float reciprocal. Reference: av1_nn_output_prec_reduce().
        const int Precision = 1 << 9;
        const float InversePrecision = (float)(1.0 / Precision);
        for (int output = 0; output < scores.Length; output++)
        {
            scores[output] = (int)((double)(scores[output] * Precision) + 0.5) * InversePrecision;
        }
    }
}
