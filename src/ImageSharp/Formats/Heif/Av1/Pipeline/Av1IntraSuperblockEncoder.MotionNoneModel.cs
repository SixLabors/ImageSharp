// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Evaluates unsplit partition termination from motion errors and completed rate/distortion costs.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Gets feature means for the 128-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone128Means =>
    [
        12.661922F, 12.638062F, 10.896497F, 10.865719F, 10.978963F, 10.940105F,
        11.012235F, 10.972760F, 11.069924F, 11.018533F, 11.773865F, 11.747426F,
        11.891315F, 11.858107F, 11.793916F, 11.766356F, 11.874997F, 11.840164F,
        5.940535F, 0.770746F, 4.292692F, 4.309581F, 0.848423F, 4.292334F,
        4.298179F, 8.514713F, 14.911736F, 19.825352F
    ];

    /// <summary>
    /// Gets feature standard deviations for the 128-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone128Deviations =>
    [
        1.796731F, 1.797056F, 1.898383F, 1.900753F, 1.846624F, 1.846953F,
        1.906632F, 1.908089F, 1.836533F, 1.835967F, 1.840262F, 1.840671F,
        1.816836F, 1.817103F, 1.879846F, 1.881333F, 1.803102F, 1.802654F,
        2.263402F, 0.420354F, 1.117165F, 1.083779F, 0.358611F, 1.101183F,
        1.084938F, 2.462638F, 1.577009F, 1.574711F
    ];

    /// <summary>
    /// Gets feature means for the 64-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone64Means =>
    [
        10.904455F, 10.853546F, 9.247903F, 9.184479F, 9.251985F, 9.186686F,
        9.253490F, 9.190190F, 9.270079F, 9.204357F, 10.086511F, 10.031060F,
        10.100875F, 10.045429F, 10.069688F, 10.013173F, 10.082980F, 10.024640F,
        4.888378F, 0.878113F, 3.598450F, 3.628491F, 0.925833F, 3.560971F,
        3.573322F, 8.807137F, 13.348477F, 18.269117F
    ];

    /// <summary>
    /// Gets feature standard deviations for the 64-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone64Deviations =>
    [
        1.789300F, 1.787061F, 1.823519F, 1.820226F, 1.794643F, 1.788620F,
        1.797194F, 1.795135F, 1.777795F, 1.773634F, 1.794000F, 1.790377F,
        1.772197F, 1.769692F, 1.819050F, 1.817139F, 1.793577F, 1.789333F,
        1.998251F, 0.327156F, 0.885748F, 0.853767F, 0.262043F, 0.902435F,
        0.860033F, 1.224865F, 1.603411F, 1.589296F
    ];

    /// <summary>
    /// Gets feature means for the 32-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone32Means =>
    [
        9.818970F, 9.751199F, 8.015079F, 7.927318F, 8.029113F, 7.938330F,
        8.012570F, 7.923719F, 8.033508F, 7.941911F, 8.933057F, 8.857422F,
        8.935639F, 8.859187F, 8.905495F, 8.829741F, 8.929428F, 8.851351F,
        4.114069F, 0.954752F, 2.645082F, 2.709703F, 0.964678F, 2.652077F,
        2.673393F, 9.430499F, 11.922798F, 16.942251F
    ];

    /// <summary>
    /// Gets feature standard deviations for the 32-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone32Deviations =>
    [
        1.737107F, 1.734327F, 1.727923F, 1.720244F, 1.721570F, 1.712775F,
        1.718028F, 1.710370F, 1.711612F, 1.702596F, 1.754856F, 1.748855F,
        1.741871F, 1.736304F, 1.722428F, 1.717380F, 1.713563F, 1.707582F,
        1.761170F, 0.207847F, 0.900058F, 0.862356F, 0.184593F, 0.903822F,
        0.856120F, 1.529199F, 1.412085F, 1.453153F
    ];

    /// <summary>
    /// Gets feature means for the 16-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone16Means =>
    [
        8.998877F, 8.912468F, 7.085255F, 6.953476F, 7.086386F, 6.954091F,
        7.088727F, 6.955747F, 7.093955F, 6.960635F, 8.065050F, 7.961432F,
        8.071631F, 7.967233F, 8.041699F, 7.937715F, 8.046791F, 7.942183F,
        3.833521F, 0.978421F, 1.901347F, 1.950124F, 0.979418F, 1.928000F,
        1.936727F, 9.773951F, 10.735227F, 15.949769F
    ];

    /// <summary>
    /// Gets feature standard deviations for the 16-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone16Deviations =>
    [
        1.641193F, 1.640172F, 1.614794F, 1.608906F, 1.609571F, 1.603580F,
        1.606928F, 1.601246F, 1.599230F, 1.593529F, 1.633747F, 1.630219F,
        1.625695F, 1.622547F, 1.633827F, 1.630182F, 1.626607F, 1.622777F,
        1.548838F, 0.145303F, 0.744550F, 0.736552F, 0.141980F, 0.742979F,
        0.736977F, 1.366255F, 1.258794F, 1.294309F
    ];

    /// <summary>
    /// Gets linear weights followed by the output bias for the 128-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone128Weights =>
    [
        -0.6106842357F, -1.0402954455F, 0.6054417656F, -0.2116623578F, 0.2447714930F, 0.3782256209F,
        0.5095592479F, -0.3275620904F, 0.3886188013F, 0.2629499420F, -0.1979599415F, -0.5389565605F,
        0.1209207902F, -0.4913347466F, 0.3798542731F, -0.2812861709F, -0.1049824167F, -0.1088672020F,
        0.4059596517F, -0.1347896613F, 0.2276868621F, 0.0506386970F, 0.0071088411F, 0.0467952100F,
        0.2091247458F, -0.7371964736F, 0.1368935545F, 0.3175247786F, -0.5493146094F
    ];

    /// <summary>
    /// Gets linear weights followed by the output bias for the 64-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone64Weights =>
    [
        -0.4150046575F, -0.3954358561F, 0.1997997444F, 0.3395826831F, 0.2827215753F, 0.3395683652F,
        0.2483140395F, 0.2722216476F, 0.2610308009F, 0.3724974359F, -0.0551479654F, -0.1721616359F,
        -0.3459358629F, -0.0952524186F, -0.1428993840F, -0.0415654914F, -0.3169539902F, -0.0269429900F,
        0.9891530919F, -0.0125084982F, 0.0972182377F, 0.0008889801F, 0.0205418050F, 0.0057237854F,
        0.1005222691F, -0.2851321920F, -1.5150336445F, 0.1893942436F, -0.4337360901F
    ];

    /// <summary>
    /// Gets linear weights followed by the output bias for the 32-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone32Weights =>
    [
        -0.4667392852F, -0.3893302767F, 0.1603498635F, 0.2304974726F, 0.1404975592F, 0.2505516225F,
        0.1423053884F, 0.2189318406F, 0.1379765409F, 0.2638241296F, -0.1342865463F, -0.0549054345F,
        -0.1925223436F, -0.1142702769F, 0.0127811659F, 0.0868639997F, -0.0643197251F, 0.0279496470F,
        0.9904395769F, -0.0095178685F, 0.1179410649F, -0.0013411972F, 0.0095060660F, 0.0195730400F,
        0.0779717771F, -0.2498860763F, -0.8168817125F, -0.4798397348F, -0.6609679881F
    ];

    /// <summary>
    /// Gets linear weights followed by the output bias for the 16-sample unsplit model.
    /// </summary>
    private static ReadOnlySpan<float> MotionNone16Weights =>
    [
        -0.3021081992F, -0.4620153673F, 0.0448577479F, 0.1738455035F, 0.0663209177F, 0.1629614573F,
        0.0555168744F, 0.1631870212F, 0.0425805150F, 0.1688564954F, 0.0434083772F, -0.0046603915F,
        -0.0271580056F, -0.0183879127F, 0.1073730471F, 0.0314201476F, 0.0576891756F, 0.0119723753F,
        0.9084332022F, -0.0188429077F, 0.0755089811F, -0.0172550234F, 0.0037663075F, 0.0022094472F,
        0.0500247894F, -0.2944572004F, -0.8908521199F, -0.2555515792F, -0.5396254205F
    ];

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Decides whether the completed unsplit candidate makes additional partition searches unnecessary.
        /// </summary>
        /// <param name="motionSearchPrediction">The prediction buffer of the motion search.</param>
        /// <param name="filterRows">The intermediate rows of the prediction filters.</param>
        /// <param name="motionVectorCosts">The motion vector rates of the frame precision.</param>
        /// <param name="modeInfoGrid">The mode-information allocation-index grid of the picture.</param>
        /// <param name="modeInfoAllocation">The mode-information values of the picture.</param>
        /// <param name="workspaceStorage">The storage of the block workspace, which holds the search buffers of every block.</param>
        /// <param name="macroBlock">The neighbor availability of the block.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="nodeIndex">The simple motion node of the block.</param>
        /// <param name="statistics">The rate and distortion of the unsplit block.</param>
        /// <returns><see langword="true"/> when the remaining partition searches stop.</returns>
        private bool ShouldTerminateAfterMotionNone(
            Span<TSample> motionSearchPrediction,
            Span<short> filterRows,
            in Av1MotionVectorCosts motionVectorCosts,
            ReadOnlySpan<int> modeInfoGrid,
            Span<Av1MacroBlockModeInfo> modeInfoAllocation,
            Span<int> workspaceStorage,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            Av1BlockSize blockSize,
            int nodeIndex,
            Av1RateDistortionStatistics statistics)
        {
            ReadOnlySpan<float> means;
            ReadOnlySpan<float> deviations;
            ReadOnlySpan<float> weights;
            switch (blockSize)
            {
                case Av1BlockSize.Block128x128:
                    means = MotionNone128Means;
                    deviations = MotionNone128Deviations;
                    weights = MotionNone128Weights;
                    break;
                case Av1BlockSize.Block64x64:
                    means = MotionNone64Means;
                    deviations = MotionNone64Deviations;
                    weights = MotionNone64Weights;
                    break;
                case Av1BlockSize.Block32x32:
                    means = MotionNone32Means;
                    deviations = MotionNone32Deviations;
                    weights = MotionNone32Weights;
                    break;
                case Av1BlockSize.Block16x16:
                    means = MotionNone16Means;
                    deviations = MotionNone16Deviations;
                    weights = MotionNone16Weights;
                    break;
                default:
                    return false;
            }

            InlineArray32<float> featureStorage = default;
            Span<float> features = featureStorage;
            this.GetSimpleMotionFeatures(
                motionSearchPrediction,
                filterRows,
                in motionVectorCosts,
                modeInfoGrid,
                modeInfoAllocation,
                workspaceStorage,
                macroBlock,
                blockOrigin,
                blockSize,
                nodeIndex,
                true,
                features);

            features[25] = float.LogP1(statistics.Rate);
            features[26] = float.LogP1(statistics.Distortion);
            features[27] = float.LogP1(statistics.Cost);
            float score = 0F;
            for (int index = 0; index < 28; index++)
            {
                // Multiply before division to retain the fitted linear model's evaluation order.
                score += weights[index] * (features[index] - means[index]) / deviations[index];
            }

            score += weights[28];
            return score >= 0F;
        }
    }
}
