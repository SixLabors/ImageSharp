// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Predicts a superblock's maximum partition size from full-sample motion measurements.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Gets the logits kernel data in output-node/input-feature order.
    /// </summary>
    private static ReadOnlySpan<float> MaximumPartitionLogitsKernel =>
    [
        -0.304561F, 0.0885596F, -0.988539F, 1.08147F, 0.215213F, 0.202965F, -0.828457F,
        -0.233945F, -0.0866977F, -0.115521F, 0.02079F, 0.196491F, -0.0285075F, 0.05067F,
        -0.00872862F, 0.00281844F, -0.238954F, 0.0253801F, 0.0257775F, 0.339269F, 0.176174F,
        -0.152545F, -0.0588704F, -1.62275F, -0.189329F, 0.0808033F, 0.233844F, -4.53798F,
        0.674968F, -0.0361688F, -0.0754075F, 1.16129F, -0.0188879F, 0.113255F, -3.04378F,
        0.814728F, -0.568517F, -0.00179383F, -3.61223F, -1.67535F, -2.20417F, -0.197196F,
        0.0507745F, -0.0909394F, -0.0507879F, -1.27999F, -0.055623F, 0.0318497F, 0.192867F,
        0.138726F, 0.0443392F, -0.595075F, -0.166774F, 0.0882958F, -0.348161F, 0.0214428F,
        -0.0599275F, -0.0995385F, -0.82358F, 0.141205F, -0.053232F, 0.00508296F, -1.90872F,
        1.15004F, -0.194219F, 0.0229019F, -0.00354318F, 0.22016F, 0.154101F, -0.159231F,
        -0.0446647F, -0.197503F, 0.0408453F, 0.197659F, 0.797858F, -0.189722F, 0.343653F,
        0.124666F, -1.03083F, 0.603059F, 0.101565F, 0.0932993F, 0.462484F, 0.295984F,
        1.11198F, 0.143709F, -0.846232F, -0.464392F, -1.06058F, -0.124889F, 0.0727475F,
        1.18446F, -0.100302F, 0.0641918F, -0.101622F, 0.10219F, 0.130189F, 0.0915623F,
        -0.166904F, -1.10606F, -0.16726F, -0.146152F, 0.145443F, -0.177091F, -0.0215214F,
        0.0158506F, -0.553294F, 0.0784749F, -0.0416628F, -0.027785F, 0.280027F, 0.484898F,
        -0.164225F, 0.0238317F, -0.0345254F, 0.0410244F, 0.131529F, 0.0239622F, -0.0749436F,
        -0.0224914F, 0.128926F, 0.224539F, 0.413297F, 0.0638572F, 0.103308F, 0.0913242F,
        -0.119274F, 0.0163103F, 0.113828F, 0.119809F, 0.297057F, -0.124889F, -0.533108F,
        -0.181408F, -0.129896F, 0.0221064F, -0.0773281F, -0.0386467F, 0.0342961F, 0.126575F,
        -0.24114F, 0.0735576F, 0.0524791F, 0.246896F, -0.130674F, -0.03979F, 0.173639F,
        1.95193F, -0.113029F, -0.0305852F, -0.00671737F, 0.157159F, -0.00102858F, -0.543688F,
        0.566772F, 0.124124F, -0.0294064F, -0.0699021F, -0.0704103F, -0.766097F, -0.0625802F,
        -0.0906173F, -0.0520414F, -0.0272724F, 0.283064F, 0.236213F, -0.127319F, 0.019392F,
        0.170042F, -0.0214542F, 0.0740938F, 0.356578F, -0.236257F, 0.269021F, 0.114759F,
        -0.641166F, 0.136308F, -0.0386959F, -0.112024F, -0.361209F, 0.686095F, 0.183906F,
        0.288656F, 0.182007F, 0.337458F, 0.058974F, -0.305512F, -0.841708F, -0.243779F,
        -0.0614058F, 0.208747F, 0.448697F
    ];

    /// <summary>
    /// Gets the layer 0 bias data in output-node/input-feature order.
    /// </summary>
    private static ReadOnlySpan<float> MaximumPartitionLayer0Bias =>
    [
        -0.776544F, -2.0022F, -0.330294F, 2.47665F, 1.90206F, -1.61571F, 0.536246F,
        1.00455F, 5.24561F, 1.55111F, -0.816399F, -4.88703F, -1.06417F, -1.15359F,
        -0.145289F, 1.91831F, 0.630915F, -1.94256F, -3.35239F, -1.05007F, -1.05186F,
        1.36824F, -5.2878F, 1.10482F, -5.00077F, -0.0445198F, 3.41427F, 2.3439F,
        -0.413306F, -1.88152F, -2.28638F, 8.24783F, -1.91961F, -1.49324F, 1.96599F,
        -6.32309F, -0.332426F, -0.425506F, 4.06511F, 5.84386F, 4.15747F, 1.22402F,
        2.8512F, 2.53027F, 0.0170272F, -1.43966F, -0.997785F, 5.43064F
    ];

    /// <summary>
    /// Gets the logits bias data in output-node/input-feature order.
    /// </summary>
    private static ReadOnlySpan<float> MaximumPartitionLogitsBias =>
    [
        -4.25432F, 0.144758F, 1.96217F, 0.728905F
    ];

    /// <summary>
    /// Gets the layer 0 kernel data in output-node/input-feature order.
    /// </summary>
    private static ReadOnlySpan<float> MaximumPartitionLayer0Kernel =>
    [
        0.992471F, 0.533006F, 0.143743F, -2.51788F, -0.468337F, -0.201376F, -0.151834F,
        0.479883F, 1.16061F, -0.278878F, -0.814954F, -0.152405F, -0.0521608F, 0.797104F,
        -2.08912F, 0.385839F, -2.22889F, -0.106858F, -0.239766F, -0.951128F, -0.698753F,
        0.0831051F, 1.1702F, 0.342834F, -0.0352795F, -0.0847639F, -0.802086F, 0.258982F,
        1.14174F, 0.645885F, -1.19226F, -0.592888F, -0.343659F, 1.1912F, 1.45411F,
        -1.22927F, 0.152858F, 0.00373585F, -1.60637F, 0.592611F, 0.0857475F, -0.346147F,
        -0.150784F, -0.0817408F, -0.189918F, -0.804952F, -1.33036F, -1.03307F, 0.0248769F,
        0.16607F, -2.896F, -2.1293F, 0.12293F, -0.173179F, -0.212128F, -6.76221F,
        0.033188F, 0.0231787F, 0.905957F, 0.0551327F, -0.356276F, 0.0181795F, 0.0977523F,
        -0.0352873F, -0.0396386F, 2.3241F, 0.0632874F, -0.11804F, -6.32521F, 0.0224659F,
        -0.00188896F, 0.267992F, 0.272337F, 0.00936963F, 0.659969F, -2.25707F, -0.0278229F,
        -0.0185089F, -1.14466F, 0.104827F, 0.0435885F, 0.558586F, -0.00697004F, 0.0312611F,
        0.540574F, -0.568625F, 0.218608F, 0.378911F, -0.0289192F, -0.0734742F, -1.08782F,
        -2.42069F, -0.0127239F, 0.0493651F, -1.15837F, 0.261831F, 0.401824F, -1.04545F,
        0.284173F, 0.784972F, -0.511243F, -0.982599F, -0.106134F, -0.325964F, -1.44107F,
        -1.42434F, -1.02402F, -1.52034F, 0.0737116F, 0.0462242F, 0.628722F, -1.0405F,
        -0.113718F, 2.20573F, -4.33951F, -0.0192695F, -0.0229314F, -1.89156F, 0.645942F,
        0.375708F, -1.97447F, -0.267014F, 0.0989443F, -0.450534F, -1.01737F, -0.642416F,
        -0.0897288F, -2.08724F, -0.190965F, -0.279135F, -0.830178F, 0.808754F, -0.139091F,
        1.11004F, -0.454439F, -0.479238F, -1.44001F, 0.0888059F, 0.885689F, -0.642505F,
        -0.00773651F, -0.0265721F, -0.906346F, 1.68504F, 0.084257F, -0.951101F, -8.06495F,
        0.19231F, 0.16389F, -0.193678F, 0.729837F, -1.98392F, -5.98513F, 3.32638F,
        -0.0658378F, -0.0910426F, -0.666567F, -0.315339F, 0.123124F, -2.66375F, -0.714852F,
        -0.136176F, -0.460166F, -0.567551F, -1.06193F, -1.21389F, -0.83865F, 0.00280695F,
        -0.199519F, -0.534704F, 0.419311F, -0.149008F, -3.68707F, 0.00285113F, -0.0718198F,
        -1.41026F, -1.34155F, -0.538687F, -0.623666F, -2.56462F, -0.0183333F, -0.323532F,
        -1.27141F, -0.0212039F, 0.198633F, 0.459554F, -4.65103F, -1.01293F, -1.39512F,
        -0.289026F, 0.208724F, -0.665226F, 1.13369F, -1.96734F, -1.45442F, -3.46172F,
        0.810681F, -0.603973F, 0.842764F, -3.90371F, -0.394561F, -3.61363F, -2.88085F,
        0.031645F, -0.23125F, -2.63898F, -1.35314F, -0.46726F, 1.33145F, 1.20269F,
        1.38682F, -0.331637F, 0.069021F, 0.149523F, -1.24957F, -0.878857F, -0.200368F,
        0.465744F, 1.01365F, -0.0122221F, -0.550586F, -1.12581F, -0.422132F, -0.0744868F,
        -2.4804F, -1.07072F, -0.479006F, 0.101817F, -0.118947F, 0.341576F, -1.0538F,
        -0.812346F, -1.13727F, -0.00939806F, 10.1571F, -0.0441302F, 0.00280407F, -21.5044F,
        0.0181152F, -0.0143246F, 3.23462F, -1.38624F, -1.80416F, 4.89763F, -2.67364F,
        2.31771e-05F, 0.000393989F, 0.352204F, -0.193455F, 0.531455F, 0.488757F, -0.442555F,
        -0.518528F, 0.431482F, -2.67727F, -2.00626F, -0.39729F, -0.221494F, -0.0188888F,
        -0.0377649F, -1.80169F, 0.0810332F, -0.0408335F, -1.28675F, -0.0353824F, -0.666723F,
        -1.07281F, 0.252912F, -1.24547F, -1.7831F, -1.14354F, -0.137662F, 0.00230182F,
        0.736862F, 0.175872F, -0.187556F, 0.43963F, -0.796524F, 0.056219F, -0.387874F,
        0.0710224F, -0.16548F, -0.100993F, 0.931481F, -3.20738F, -0.0197576F, 0.266148F,
        -0.173909F, -0.337795F, -0.0682381F, 0.176844F, 0.140286F, 1.12033F, 0.429064F,
        -2.24192F, -1.54682F, 2.23646F, -0.0371138F, -0.0475339F, -3.21766F, 0.0412858F,
        0.387811F, 6.6711F, 0.140649F, 0.0559547F, -0.802839F, 0.599977F, 0.64552F,
        -2.08103F, -0.503401F, -0.0407036F, -0.0299199F, 0.0849445F, -0.111657F, -1.63462F,
        3.33762F, 0.0441394F, 0.0466889F, -0.951806F, 0.0723954F, 0.00348661F, -1.36903F,
        2.24625F, -0.0348915F, -0.0508893F, -0.240891F, -0.120143F, -0.17991F, -2.09137F,
        0.0150871F, 0.0480333F, 1.72012F, 0.0309551F, -0.0370507F, -0.377075F, 0.103916F,
        -0.0169255F, -0.0145395F, -4.02144F, 0.83193F, -0.316502F, 6.3832F, -1.70038F,
        -1.97215F, -1.94501F, 1.45479F, 0.711725F, -0.348496F, -0.279056F, -1.13396F,
        -1.51744F, -0.853307F, 1.53131F, -0.0032358F, 1.41808F, -1.32989F, -0.245221F,
        -0.161614F, -0.500845F, -0.449252F, 0.0724151F, -0.116333F, -0.0946182F, -2.0945F,
        0.0564572F, 0.393261F, -1.06861F, -0.111458F, -0.839943F, -0.0880348F, 0.0365742F,
        0.415339F, -1.57494F, -0.713697F, 1.02349F, -0.221371F, -0.0446281F, 1.89223F,
        -0.0811754F, -0.402773F, -0.930987F, 0.0243194F, 0.0678332F, -0.0233014F, 0.165372F,
        -0.44083F, -1.2404F, 0.35675F, -0.040916F, -0.0512548F, -2.9071F, 0.861174F,
        -0.778133F, 2.14436F, -0.688427F, -0.480371F, -1.69032F, 0.706687F, -0.281982F,
        -2.30451F, 1.61541F, -0.0213638F, -0.740509F, -0.266677F, 0.0268434F, -0.0116908F,
        -3.17595F, 0.0114825F, 0.0196997F, -0.144005F, 0.0550181F, -0.851459F, -0.000285073F,
        -0.538441F, -0.0254868F, -0.0104454F, -0.0661998F, -0.196469F, -0.346372F, -5.52892F,
        -0.643683F, -0.622224F, -0.31463F, -0.555956F, -0.520132F, -0.843166F, -2.59479F,
        -0.750195F, 0.00635995F, -0.338615F, -0.216676F, -0.391544F, -1.62185F, -0.718471F,
        -0.475406F, -0.782041F, -0.608824F, -1.09633F, -1.27308F, -0.560719F, -0.207539F,
        -0.0196445F, -1.05519F, -0.575249F, -1.0642F, 1.01615F, -0.873633F, -0.417953F,
        -0.428051F, 0.350259F, -2.53833F, -2.72203F, 0.672846F, -0.503094F, -1.1374F,
        0.214291F, 0.013305F, 0.0112064F, 1.10532F, 0.030455F, 0.0239614F, 0.628072F,
        0.0539135F, -0.472441F, -0.688439F, -0.32044F, -0.0234867F, -0.0158436F, -0.949314F,
        -0.0453161F, -1.18306F, 0.626845F, -0.426925F, -0.688371F, 0.415062F, 0.0640985F,
        -0.638387F, -2.01399F, -0.209744F, -0.762892F, -0.0753296F, -0.879315F, -0.520433F,
        -0.111375F, 0.389742F, -0.398862F, -0.643227F, -0.246396F, 0.0317051F, 1.06973F,
        0.413617F, 0.180506F, -0.0507897F, -0.00650435F, 0.620892F, 0.046312F, 0.475032F,
        0.906993F, -0.0388061F, -0.256271F, -1.03323F, 0.0125266F, -0.31116F, -0.377611F,
        -0.0386407F, -0.0232745F, -0.353644F, -2.27289F, 0.0571779F, -0.00865006F, 1.65101F,
        0.0175711F, 0.0184585F, 0.558458F, 0.2213F, -0.285089F, 0.433445F, -0.427177F,
        -0.0103682F, -0.0101273F, 0.214085F, -0.0459885F, 0.00761981F, 0.836381F, 0.0175293F,
        0.02508F, -1.51778F, 0.0143956F, -0.162589F, 0.595418F, 0.21445F, -0.0335848F,
        -0.0136684F, -0.16686F, -0.14612F, 0.0816238F, 0.499636F, 0.12458F, -2.41673F,
        -0.261721F, -0.676805F, -1.88366F, 0.730462F, 0.69196F, -0.0288489F, -2.38272F,
        0.329876F, 0.014517F, -0.115145F, -3.48151F, -0.00209072F, -0.0732377F, 0.820443F,
        -0.0118701F, 0.112145F, 0.272315F, 0.137531F, -0.0200997F, -0.0397883F, -2.19458F,
        0.183554F, -0.639716F, 0.481605F, -0.621639F, -0.0980299F, -0.710534F, -0.143105F,
        -6.77626F, -1.65139F, -2.37718F, -0.533127F, -1.12574F, 3.34182F, -0.0758663F,
        0.0334238F, -9.48647F, 0.0674974F, 0.0507665F, 0.523007F, -0.0668F, 0.5736F,
        -0.589761F, -1.1692F, -0.0236497F, -0.00828928F, -0.265823F, 1.15284F, 0.307927F,
        -0.695308F, 0.13725F, -0.20394F, -0.363965F, -0.331159F, -1.50927F, -1.20051F,
        -0.0205825F, -0.0381859F, -0.0579876F, -1.6913F, -1.94626F, 3.4214F, 3.3922F,
        -2.13798F, -0.679848F, -0.890735F, 0.235017F, -0.253202F, -1.0571F, 1.40354F,
        0.00719052F, -1.54365F, -0.7289F, -1.05492F, 0.0238169F, -0.00543592F, -0.0510353F,
        -0.175386F, -0.724207F, -0.788936F, 0.039976F, 1.36966F, 0.869475F, -0.0302774F,
        -0.0537556F
    ];

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Aggregates the 64 full-sample block searches needed to choose a 128-sample superblock's size limit.
        /// </summary>
        private Av1BlockSize PredictMaximumPartition(Point blockOrigin)
        {
            float rowSum = 0F;
            float rowSquares = 0F;
            float rowMinimum = float.MaxValue;
            float rowMaximum = 0F;
            float columnSum = 0F;
            float columnSquares = 0F;
            float columnMinimum = float.MaxValue;
            float columnMaximum = 0F;
            float errorSum = 0F;
            float errorSquares = 0F;
            float errorMinimum = float.MaxValue;
            float errorMaximum = 0F;
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    Point origin = new(blockOrigin.X + (column * 16), blockOrigin.Y + (row * 16));
                    Av1MotionVector vector = this.SearchSimpleMotion(origin, Av1BlockSize.Block16x16, default, false, out int squaredError, out _);
                    float motionRow = vector.Row / 8;
                    float motionColumn = vector.Column / 8;
                    float logError = float.LogP1(squaredError);
                    rowSum += motionRow;
                    rowSquares += motionRow * motionRow;
                    rowMinimum = Math.Min(rowMinimum, Math.Abs(motionRow));
                    rowMaximum = Math.Max(rowMaximum, Math.Abs(motionRow));
                    columnSum += motionColumn;
                    columnSquares += motionColumn * motionColumn;
                    columnMinimum = Math.Min(columnMinimum, Math.Abs(motionColumn));
                    columnMaximum = Math.Max(columnMaximum, Math.Abs(motionColumn));
                    errorSum += logError;
                    errorSquares += logError * logError;
                    errorMinimum = Math.Min(errorMinimum, logError);
                    errorMaximum = Math.Max(errorMaximum, logError);
                }
            }

            // Aggregate in raster order, retaining single precision throughout the trained feature calculation.
            float meanRow = rowSum / 64F;
            float meanColumn = columnSum / 64F;
            float meanError = errorSum / 64F;
            int dcStep = Av1QuantizationLookup.GetDcQuant(this.superblockQIndex, 0, this.bitDepth) >> (this.bitDepth.GetBitCount() - 8);
            InlineArray16<float> features = default;
            features[0] = meanError;
            features[1] = meanColumn;
            features[2] = meanRow;
            features[3] = float.LogP1((dcStep * dcStep) / 256F);
            features[4] = columnMaximum;
            features[5] = rowMaximum;
            features[6] = errorMaximum;
            features[7] = columnMinimum;
            features[8] = rowMinimum;
            features[9] = errorMinimum;
            features[10] = (errorSquares / 64F) - (meanError * meanError);
            features[11] = (columnSquares / 64F) - (meanColumn * meanColumn);
            features[12] = (rowSquares / 64F) - (meanRow * meanRow);
            InlineArray4<float> scores = default;
            EvaluateMotionPartitionModel(
                features[..13],
                MaximumPartitionLayer0Kernel,
                MaximumPartitionLayer0Bias,
                MaximumPartitionLogitsKernel,
                MaximumPartitionLogitsBias,
                scores);

            Av1EncoderSpeedSettings.MaximumPartitionPrediction mode = this.picture.Parent.SpeedSettings.MaximumPartitionPredictionMode;
            int selected = 3;
            if (mode == Av1EncoderSpeedSettings.MaximumPartitionPrediction.Direct)
            {
                selected = 0;
                for (int index = 1; index < 4; index++)
                {
                    if (scores[index] > scores[selected])
                    {
                        selected = index;
                    }
                }
            }
            else
            {
                float maximum = Math.Max(Math.Max(scores[0], scores[1]), Math.Max(scores[2], scores[3]));
                float sum = 0F;
                for (int index = 0; index < 4; index++)
                {
                    scores[index] = MathF.Exp(Math.Max(scores[index] - maximum, -10F));
                    sum += scores[index];
                }

                for (int index = 0; index < 4; index++)
                {
                    scores[index] /= sum;
                }

                int variance = mode == Av1EncoderSpeedSettings.MaximumPartitionPrediction.Adaptive
                    ? this.GetSourceVariance(blockOrigin, Av1BlockSize.Block128x128) : 0;

                if (mode == Av1EncoderSpeedSettings.MaximumPartitionPrediction.Relaxed || variance > 16)
                {
                    double threshold = mode == Av1EncoderSpeedSettings.MaximumPartitionPrediction.Relaxed ? 0.2 : variance < 128 ? 0.05 : 0.1;
                    for (; selected >= 0; selected--)
                    {
                        if (selected < 3)
                        {
                            scores[selected] += scores[selected + 1];
                        }

                        if (scores[selected] > threshold)
                        {
                            break;
                        }
                    }
                }
            }

            return (Av1BlockSize)((selected + 2) * 3);
        }
    }
}
