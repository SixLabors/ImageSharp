// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The motion vector statistics of the last coded inter frame, and the network that chooses the motion vector
/// precision of the next inter frame from them. Reference: MV_STATS, av1_collect_mv_stats(), get_smart_mv_prec() and
/// av1_pick_and_set_high_precision_mv().
/// </summary>
internal sealed partial class Av1MotionVectorStatistics
{
    /// <summary>
    /// The number of network features. Reference: MV_PREC_FEATURE_SIZE.
    /// </summary>
    private const int FeatureCount = 18;

    /// <summary>
    /// The number of hidden network nodes. Reference: MV_PREC_LAYER_SIZE_0.
    /// </summary>
    private const int HiddenCount = 32;

    /// <summary>
    /// The quantizer below which a frame uses eighth-sample vectors without statistics. Reference:
    /// HIGH_PRECISION_MV_QTHRESH.
    /// </summary>
    private const int HighPrecisionQThreshold = 128;

    /// <summary>
    /// The number of vector joint types. Reference: MV_JOINTS.
    /// </summary>
    private const int JointCount = 4;

    /// <summary>
    /// The default motion vector distributions, read by a frame without a primary reference.
    /// </summary>
    private static readonly Av1MotionVectorContext DefaultContext = new();

    /// <summary>
    /// The distributions the collection prices vectors with. Every tile restarts them from the frame context.
    /// </summary>
    private readonly Av1MotionVectorContext context = new();

    /// <summary>
    /// The number of vectors of each joint type. Reference: mv_joint_count.
    /// </summary>
    private readonly int[] jointCounts = new int[JointCount];

    /// <summary>
    /// The inter blocks whose source texture the frame completion measures.
    /// </summary>
    private readonly List<TextureBlock> textureBlocks = [];

    private int q;
    private int order;
    private bool valid;
    private bool useHighPrecision;
    private int interCount;
    private int intraCount;
    private int defaultMotionVectors;
    private int lastBitZero;
    private int lastBitNonzero;
    private int totalRate;
    private int highPrecisionTotalRate;
    private int lowPrecisionTotalRate;
    private int horizontalTexture;
    private int verticalTexture;
    private int diagonalTexture;

    /// <summary>
    /// Gets the network input means. Reference: av1_mv_prec_mean.
    /// </summary>
    private static ReadOnlySpan<float> FeatureMeans =>
    [
        143.67358891063745f, 141.6251917346238f, 0.36313633945679064f,
        0.0028162791958822085f, 0.000484820537626698f, 0.002769969388939025f,
        0.0f, 0.00031274626720947577f, 0.00020578555375160075f,
        0.0007075246732697733f, 0.000539641029909925f, 0.0013939401375906984f,
        4.985394760423499f, 4.985394760423499f, 4.9992148717283085f,
        5.143739822380163f, 5.518483124004564f, 87.63597847427077f
    ];

    /// <summary>
    /// Gets the network input deviations. Reference: av1_mv_prec_std.
    /// </summary>
    private static ReadOnlySpan<float> FeatureDeviations =>
    [
        66.86256140247244f, 68.04472572607503f, 13.23247674430399f,
        0.0029123438396921955f, 0.0015331406169374737f, 0.0029149813096313775f,
        1.0f, 0.00047501102871357813f, 0.00030025962993117947f,
        0.0009861163580391207f, 0.0012157593528004055f, 0.002004954948490521f,
        6.539447500484038f, 6.539447500484038f, 6.396589058279465f,
        3.4870155874262516f, 3.8911353973740535f, 112.07985259573601f
    ];

    /// <summary>
    /// Gets the hidden layer weights, one row of eighteen weights per node. Reference: av1_mv_prec_nn_weights_layer_0.
    /// </summary>
    private static ReadOnlySpan<float> HiddenWeights =>
    [
        -0.13008492159557145f, -0.1483527373474774f, 0.08112076098858864f, -0.9582568679627453f, -0.34794757171071206f, 0.6465225723304947f,
        0.0f, 0.06754171885839604f, 0.27156803620541214f, 0.10635231245664407f, -0.031183926995968583f, 0.048122572260291f,
        -0.19498534230045128f, -0.2614116319273316f, -0.3223762845136331f, -1.2063368350609205f, -0.523333556911706f, 1.075632260890728f,
        0.48989726814387946f, -0.34816466111070477f, 0.41668357610256473f, -1.0973562848791671f, 0.04183921854389494f, -0.9123815389260476f,
        0.0f, 0.859965047744027f, 0.1962095804679813f, 0.2606564339077058f, 0.26695868715184895f, 0.5319308568326692f,
        -0.23717505799723165f, -0.43127224481782567f, -0.3214545776203726f, 0.5850852241402176f, -0.26705531612587813f, -0.5786016766610093f,
        0.9360519909983003f, 0.20771329289016555f, -0.027614159544811823f, -1.175022807046164f, -0.07578967497693835f, 0.6890172485324256f,
        0.0f, -0.008008338164988263f, -0.08064800010158935f, -0.22606910981666667f, 0.4541586669210879f, 0.07731527661370792f,
        -0.6744475941247964f, -0.2625842448396184f, 1.7018613444303785f, -0.08622229073162656f, 0.041858142814941275f, -0.24575964090386415f,
        -0.046626044730994964f, 0.7608713064175202f, -0.23330119070907146f, -0.10115510984500826f, 0.9722537349192069f, 0.11718554254290829f,
        0.0f, 0.2075123446014759f, 0.09465167310768637f, 0.7609896851963016f, 0.4441038581385328f, 0.26064144727430955f,
        -0.14678625366485035f, -0.03597014452200524f, 0.3128680867196166f, 1.102496797385966f, 0.06642253233084111f, -1.2665494483407629f,
        0.09049412632000911f, -1.1160621999565095f, 0.043420275255913035f, -0.8811412259978966f, 0.21076234632287777f, 0.16571534463543866f,
        0.0f, -0.7324075176473275f, -0.3677622514459495f, 0.3273532243056415f, 0.22922161936797775f, 0.8204766691058087f,
        0.02982161033720488f, 0.5266419954188112f, -1.0032154963302191f, 0.7007602969763729f, 0.37196355167990885f, -0.7608579453228548f,
        0.08568111584781847f, 0.07011061059123677f, 0.3233263598082507f, -0.08249928295410253f, 0.08220165761319252f, 0.22148722752246794f,
        0.0f, 0.6122392701743506f, -0.26429838296378333f, 0.31958081620005463f, -0.006027177397853826f, -0.3088310785887994f,
        -0.5436192046707807f, -0.011080356757423306f, 0.12632650770008413f, -0.45097913215234525f, 1.8008072867127298f, -0.7630029654575501f,
        -0.4054774329826579f, 0.40386074452544535f, -0.18541426257453025f, 0.2444879765079863f, -0.6216724756115081f, 0.27030299321302f,
        0.0f, -0.6835848952967989f, -0.7914184320964815f, -0.6761595019582928f, -1.009565565604081f, -0.1904242439353305f,
        0.4463417126318631f, 0.6025503823452971f, 0.5149990860115566f, 1.0242970663937634f, 0.037947306826401385f, 0.07039339786212848f,
        0.14273796789711987f, 0.168103961425691f, 1.6596066376811978f, 0.19321092229384657f, -0.3710750388148514f, -0.01717015559410288f,
        0.0f, 0.3005688477942597f, 0.23877080653829577f, 0.2718594552971173f, 0.3885402571589898f, 0.32999531945669247f,
        -0.6134460954213243f, -0.13972265462799183f, -0.07180089575716991f, -1.014572598188105f, 0.0717207322809836f, 0.34896157745155615f,
        -0.27127687591403f, -0.5058651212773623f, -1.5442435628306925f, -0.6399784724734707f, 0.6274301429074947f, -0.4645750072767051f,
        0.0f, -0.2406726815244178f, -0.06321214115916597f, 0.312856714253404f, 0.16459514124116134f, 0.3993579604809623f,
        -0.15232044351561913f, -0.5613743948568469f, 0.7219801372223262f, 0.2936857469624009f, 0.7823466656034087f, -0.12416947814098349f,
        -0.36413756654028345f, -0.07992098796866462f, -0.7395722879842416f, 0.8639913543220514f, -0.311931773757945f, -1.7308240470400613f,
        0.0f, 0.394499716712104f, 0.6511462819539963f, -0.0722425275974144f, 0.13490818194661386f, 0.055319135836378035f,
        0.15389577508097013f, 0.28958598328870605f, -0.14608429470539772f, 0.09488817462478298f, -0.17231294096622088f, 0.6721115415911466f,
        -0.05664621150536103f, 0.03291799673669331f, 0.02845382711057482f, -0.9953563446999164f, -0.17994298220605923f, 0.6560824519337476f,
        0.0f, -0.30990646375917935f, 0.17215517202874f, 0.2026816225170481f, 0.22011958747715601f, 0.3562520768889686f,
        -0.18436559057189175f, 0.1733377147302066f, 0.02818276995640877f, -0.29703005574859076f, -0.3310652639215064f, -1.6091173258529277f,
        0.45461585790028003f, -0.5078643334592593f, -0.338997374732338f, 0.4688619590359733f, 0.627099126828289f, -0.5249801376494249f,
        0.0f, 0.34465498218272883f, 0.009891680630908135f, -0.27244020967349f, 0.05404589867626979f, -0.06220329325739666f,
        -0.13365376464759104f, -0.13098573553512366f, 0.11434198976289106f, 0.6740951247574676f, 1.3381727185724581f, -1.4865773213251936f,
        0.05809898701966341f, 0.25380780261023456f, 1.2716367496512722f, 0.1768290070780598f, -0.07554828135356352f, 0.8180570085344856f,
        0.0f, 1.0788448980077463f, 0.0651938742459459f, 0.3807672030015587f, 0.6144792680268445f, 0.011660612214908059f,
        -0.018306023765580288f, 0.44140813809926516f, -0.13411994195502386f, 0.15920368955127778f, -0.19382358417849888f, -0.08802147969690055f,
        -0.019731052733814477f, 0.1104744229169665f, -0.195834419735958f, -0.5005295046454347f, -0.17041241868229032f, -0.471942117351489f,
        0.0f, -0.3599073304761372f, -0.2745532782968519f, -0.8323064841106417f, -0.88355885384943f, -0.02826466859020679f,
        0.06977870308805256f, 0.11926112095374196f, 1.367382707959643f, -0.06119843162964051f, -0.5331395268889569f, -1.2155531584240624f,
        -0.01896651779524327f, 0.10591845408571081f, -0.010632842156504733f, 0.6150787968629282f, -0.4191690185896091f, -0.9961718918346271f,
        0.0f, 0.23370364516013867f, 0.4156033072362998f, 0.1261005546633433f, 0.0812413884532226f, -0.008894337353937203f,
        0.07984447025056046f, -0.1258098052766725f, -0.40245475467767916f, 1.78188906675019f, -1.1544387954232302f, -0.41768781481273387f,
        0.6791211165341995f, -0.4175127856183446f, -0.07353219159767788f, -0.2888813577574072f, -0.7107767892597061f, -1.0450031091195449f,
        0.0f, -0.9221599545079143f, -0.6747876356740621f, 0.30241454354872105f, 0.4924965303373908f, -0.14042722740054084f,
        0.27744210409350445f, -0.14788270997426836f, -0.9081467469237995f, -0.04513115674995093f, -0.5254168669125793f, -0.6999012037974789f,
        0.434661246306547f, -0.7193303957246092f, -0.9117952623409744f, -1.5097267865916142f, -0.20779888103770922f, 0.4935562480901218f,
        0.0f, 0.18303393908923593f, 0.34753722677570037f, 0.29291001533177663f, 0.3832351878354224f, 0.3295194956120599f,
        -0.32398033003617527f, -0.31570906736433746f, 0.23657779050372962f, 0.9510794465234161f, -0.5122243902568278f, 0.08652112725315658f,
        0.2246634353717998f, -0.9032595595582497f, -0.8936484034533545f, 0.6012969720865752f, -0.6454216646117924f, -1.1753786049658332f,
        0.0f, -0.4360545677728656f, -0.6586237455328507f, -0.34347301697886656f, -0.8909724651992144f, -0.24378721818350263f,
        0.6179733359297576f, 0.0661661181742234f, -0.14120142044993794f, -0.07732699885498932f, 1.0221355882357506f, 0.44514798994115284f,
        -0.7371569579959046f, -0.7212499572378936f, 0.7453626921081045f, 0.5478757761345768f, -0.39411232789985384f, 0.7200542656743857f,
        0.0f, -0.11790869453118827f, -0.12317030713581928f, -0.4207902738133338f, 0.15895105878327986f, 0.304261777102111f,
        0.11450744587017621f, -0.11470709991317944f, 0.5949222371739038f, 0.6549518619412444f, -0.24390606570422838f, -0.4212796009440803f,
        -0.6269666206320964f, -0.5421193969807078f, -0.12297772128652287f, 0.021517257619930424f, 0.25462855095544523f, -0.22107798187348246f,
        0.0f, 0.5204516300095662f, 0.2837402841862462f, 0.11310823283285916f, 0.8944351685018025f, 0.17487203235834015f,
        -0.5271221928634433f, -0.19516594503423199f, 0.452456617580365f, 1.2456272242706414f, 0.24166615894862817f, 0.09411429305204502f,
        -0.2730072283327243f, -0.8129383770918172f, -0.24093254193486136f, 0.5696499174142177f, -0.11110805836073044f, -0.3968204166235694f,
        0.0f, -0.04388165369378549f, -0.005631266017272595f, -0.02574211858479705f, 0.06230399626660669f, 0.17677671232932785f,
        0.5172871274400965f, 0.4919150085620063f, -1.597656637582941f, 0.02415185715719143f, -0.17945446376668306f, -0.39340600199798886f,
        0.25013205256886845f, 0.05972330340308685f, 0.1359911505596489f, -0.02341033271820833f, 0.15726074644063684f, 0.47512625913020357f,
        0.0f, 0.7327341664835779f, -0.3689092312320013f, 0.4571824787436036f, 0.6215465537945456f, 0.0944111296842023f,
        -0.12571956176607574f, -0.2507235674395462f, -0.09579602654351593f, 1.4463357293728496f, 0.749153535856049f, -0.5553955120807588f,
        -0.09622771929369946f, -0.2598697420394813f, -0.964691815299676f, -0.8289963178173902f, 0.7112949291983329f, -0.8667009730492162f,
        0.0f, -0.48698304169042794f, -0.18786095669893707f, -0.11425249263203247f, -0.3693391011684809f, 0.09933145842585253f,
        0.2568559685298844f, 0.7048512233651738f, 0.6056238412407038f, -0.4355558119826642f, 0.17318931883915484f, 0.6481333496429564f,
        -0.45728823054344486f, -0.006325004538589701f, 0.45609864075494927f, -0.6199385981116988f, 0.035105808783046165f, 0.1203147963894839f,
        0.0f, 0.383402190836527f, 0.048429009055370106f, 0.5887186439275204f, -0.20538767641607814f, -0.031237879611002117f,
        0.3140759860883231f, 0.24447070584999556f, 0.7271263905705878f, 0.8432799162434237f, -0.11530577554199217f, -0.7781023892314718f,
        0.05359488822710336f, 0.5624870388700809f, 0.5134656523208906f, 0.18304041423438375f, -0.04237421156328257f, -0.20759809886942207f,
        0.0f, -0.06249337454975615f, 0.10081284533873777f, 0.3894374350259183f, 1.518217777528342f, -0.9100037950171563f,
        0.17796906121831477f, -0.2892167255357892f, 0.6117902467884032f, 0.13332120964959573f, -0.3487155932849374f, -0.32920583745734694f,
        0.08242631209809854f, -0.24920225708110588f, 0.8401757259392635f, 0.11729108681358365f, 0.11222925752499184f, -0.027078490721459958f,
        0.0f, 0.726132375517389f, 0.72220359881096f, 0.5721582611845177f, 0.15139162075524315f, 0.6676549461551197f,
        -0.321449586554697f, -0.10141104515219895f, -0.09711123988777906f, 0.9623356184776928f, -0.7941822373167173f, -0.9373923554119346f,
        0.4573241832354059f, -0.42029139056126147f, 0.2675223459380999f, -0.5487300191551386f, 0.2236621891916084f, 0.11692039230044018f,
        0.0f, 0.1758399202780961f, 0.676447587678781f, 0.5945412815881029f, 0.5669863357359594f, 0.8433565415303922f,
        -0.30300550790708036f, -0.43332881999693673f, -0.4996522695731392f, -0.2084930815451962f, 0.27765278702463786f, 1.0886848763946915f,
        -0.0739433655813831f, -0.4762801579229192f, -0.2490825339320731f, -1.8820479350439439f, -0.4251592225775914f, -0.3992922365484464f,
        0.0f, 0.19598917760218867f, 0.4860238022746914f, 0.3364528828641281f, 0.3350950865226741f, 0.2773654548632006f,
        -0.30547262140782566f, 0.028649620490728344f, -0.11763407628280315f, 0.6237318502627169f, -0.3958952632477945f, 0.14797171297835243f,
        0.45821729624747465f, -0.8687137170773626f, 0.06989667196937126f, -0.5752606929478727f, 0.16986945686358412f, 0.6925071596817824f,
        0.0f, 0.4991250796183003f, 0.03424654896322111f, 0.6153698611882319f, 0.5070872444849457f, 0.43615747516328135f,
        -0.7870352838659244f, -0.6424101231965247f, -0.7005774876651399f, 0.79983115431488f, 0.15720357955596242f, -1.408372612176309f,
        -0.039294695217213765f, 0.6979415372962309f, 0.27403316751965656f, 1.2844596102619275f, -0.2781534150257364f, 0.3248437714908865f,
        0.0f, 0.4364362371752831f, -0.2548580911485434f, -0.19578001373349452f, -0.04597194387828005f, -0.010035156855533233f,
        0.0415941475251266f, 0.07929549739797387f, -0.060629652912508866f, 0.5977303008711333f, -1.4404008068066554f, 0.8555694790197376f,
        -0.03693438534401856f, 0.17761411164512408f, -0.11858304304109235f, -1.4241324353471327f, 0.1533849765389186f, 0.7650643783126995f,
        0.0f, -0.0639949379280401f, 0.4288617817939563f, 0.4235508646885404f, 0.3419843254383798f, -0.015992360660098768f,
        -0.773247697505441f, -0.4908452922015917f, 0.9868134897291486f, -0.5078689994742608f, 1.05632043744864f, -0.38867419409275117f,
        -0.0065547696858664194f, -0.3056003173415037f, -0.333762331930102f, 0.4459671174011671f, 0.08219092584580244f, -0.08099158579518179f,
        0.0f, -0.1568180656346373f, -0.061962372393910135f, 0.14065868174859464f, -0.055925712798972765f, 0.05136117465820622f,
        0.0907831030477633f, 0.19518110495319604f, -0.7470794578145956f, 1.5945999734733545f, -0.4351697502345834f, -0.33253649399571805f
    ];

    /// <summary>
    /// Gets the hidden layer biases. Reference: av1_mv_prec_nn_bias_layer_0.
    /// </summary>
    private static ReadOnlySpan<float> HiddenBiases =>
    [
        -0.651213833993862f, -1.1243309933417809f, -0.2123880023097051f, 0.23095477452877616f,
        -0.6668057665893545f, 0.3082268148379634f, -0.3344916753975844f, -0.20920185606857844f,
        0.6057933917964854f, 0.5031857662559803f, -1.5380096313468152f, -0.4457245344804041f,
        1.82368055812373f, 0.7973912064077963f, 0.25706500555622913f, 0.1394695119825382f,
        0.4508811973450553f, -0.5408959545111782f, 1.064829233697863f, 0.3733268644246235f,
        1.1173169029905483f, -0.2012817466400134f, -0.16628447748302294f, 1.3086000088940826f,
        0.7267092979664235f, -0.9097857006590555f, -0.7564259343863077f, -0.49844128036716173f,
        -0.4675729246975423f, -0.03626154526362181f, -0.41957330902404616f, -0.9658160514319954f
    ];

    /// <summary>
    /// Gets the output layer weights. Reference: av1_mv_prec_nn_weights_layer_1.
    /// </summary>
    private static ReadOnlySpan<float> OutputWeights =>
    [
        1.5017296484510276f, 1.044216918060133f, -1.066541411740906f, -0.7762965171172661f,
        -0.9814396609661653f, 0.9334065847340715f, 0.7117244268817873f, -0.7695942296628597f,
        0.7892157680137047f, -0.5786309358654476f, -2.4444494892027264f, 1.1666759262637185f,
        -0.9699580532370483f, 0.5849682956422552f, -1.0372272986941953f, -0.5005014627824439f,
        1.1816204711740521f, -1.2204867615892114f, 0.4510263977504913f, 0.35567865078585165f,
        -0.7811389330738839f, -0.6643977800301099f, -0.6283287371705794f, 0.790873821018048f,
        0.8861643352684585f, 0.6438840651522237f, 0.6677191546466089f, 0.9703715021995785f,
        1.250893534236489f, 0.7733742028067933f, -1.249673977776904f, -1.2890127265725608f
    ];

    /// <summary>
    /// Chooses whether an inter frame codes eighth-sample vectors: below the quantizer threshold, or by the network
    /// over the statistics of the last coded frame when the speed features read them. Reference:
    /// av1_pick_and_set_high_precision_mv() with av1_frame_allows_smart_mv().
    /// </summary>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="readsLastFrameData">Whether the speed features select LAST_MV_DATA.</param>
    /// <param name="frameAllowsSmartPrecision">Whether the frame is neither intra-only nor an overlay.</param>
    /// <param name="orderHint">The order hint of the frame.</param>
    /// <param name="frameSize">The frame dimensions.</param>
    /// <returns><see langword="true"/> when the frame uses eighth-sample vectors.</returns>
    public bool PickHighPrecision(int qIndex, bool readsLastFrameData, bool frameAllowsSmartPrecision, int orderHint, Size frameSize)
    {
        bool highPrecision = qIndex < HighPrecisionQThreshold;
        if (readsLastFrameData && frameAllowsSmartPrecision && this.valid)
        {
            highPrecision = this.GetSmartPrecision(qIndex, orderHint, frameSize);
        }

        return highPrecision;
    }

    /// <summary>
    /// Discards the statistics of an earlier frame before a frame is coded. Reference: the av1_zero(cpi->mv_stats) of
    /// encode_with_recode_loop(), which a valid set of statistics reaches after every coded frame.
    /// </summary>
    public void DiscardIfValid()
    {
        if (!this.valid)
        {
            return;
        }

        this.q = 0;
        this.order = 0;
        this.valid = false;
        this.interCount = 0;
        this.intraCount = 0;
        this.defaultMotionVectors = 0;
        this.jointCounts.AsSpan().Clear();
        this.lastBitZero = 0;
        this.lastBitNonzero = 0;
        this.totalRate = 0;
        this.highPrecisionTotalRate = 0;
        this.lowPrecisionTotalRate = 0;
        this.horizontalTexture = 0;
        this.verticalTexture = 0;
        this.diagonalTexture = 0;
    }

    /// <summary>
    /// Begins the collection over a coded frame.
    /// </summary>
    /// <param name="allowHighPrecision">Whether the frame codes eighth-sample vectors.</param>
    public void BeginFrame(bool allowHighPrecision)
    {
        this.useHighPrecision = allowHighPrecision;
        this.textureBlocks.Clear();
    }

    /// <summary>
    /// Restarts the vector distributions of the collection from the frame context at the start of a tile. Reference:
    /// the tctx = *cm->fc copy of av1_collect_mv_stats().
    /// </summary>
    /// <param name="frameContext">The motion vector distributions the frame starts from, or <see langword="null"/>
    /// for the defaults.</param>
    public void BeginTile(Av1MotionVectorContext? frameContext) => this.context.CopyFrom(frameContext ?? DefaultContext);

    /// <summary>
    /// Counts an intra block of an inter frame. Reference: the intra branch of collect_mv_stats_b().
    /// </summary>
    public void CollectIntraBlock() => this.intraCount++;

    /// <summary>
    /// Adds the statistics of an inter block, in coding order: the new vectors are priced against the adapting
    /// distributions, and the block is kept for the texture measure. Reference: collect_mv_stats_b() with
    /// get_ref_mv_for_mv_stats().
    /// </summary>
    /// <param name="mode">The prediction mode.</param>
    /// <param name="isCompound">Whether the block predicts from two references.</param>
    /// <param name="primaryReference">The reference vector of the first new vector.</param>
    /// <param name="primaryVector">The first vector of the block.</param>
    /// <param name="secondaryReference">The reference vector of the second new vector.</param>
    /// <param name="secondaryVector">The second vector of the block.</param>
    /// <param name="origin">The luma origin of the block.</param>
    /// <param name="blockSize">The block size.</param>
    public void CollectInterBlock(
        Av1PredictionMode mode,
        bool isCompound,
        Av1MotionVector primaryReference,
        Av1MotionVector primaryVector,
        Av1MotionVector secondaryReference,
        Av1MotionVector secondaryVector,
        Point origin,
        Av1BlockSize blockSize)
    {
        this.interCount++;
        if (mode is Av1PredictionMode.NewMotionVector or Av1PredictionMode.NewNewMotionVector)
        {
            this.KeepMotionVector(primaryReference, primaryVector);
            if (isCompound)
            {
                this.KeepMotionVector(secondaryReference, secondaryVector);
            }
        }
        else if (mode is Av1PredictionMode.NearestNewMotionVector or Av1PredictionMode.NearNewMotionVector)
        {
            this.defaultMotionVectors++;
            this.KeepMotionVector(secondaryReference, secondaryVector);
        }
        else if (mode is Av1PredictionMode.NewNearestMotionVector or Av1PredictionMode.NewNearMotionVector)
        {
            this.defaultMotionVectors++;
            this.KeepMotionVector(primaryReference, primaryVector);
        }
        else
        {
            this.defaultMotionVectors += isCompound ? 2 : 1;
        }

        this.textureBlocks.Add(new TextureBlock(origin, blockSize));
    }

    /// <summary>
    /// Completes the statistics of a coded frame: the source texture of its inter blocks, its quantizer and its order
    /// hint. Reference: the texture sums of collect_mv_stats_b() and the end of av1_collect_mv_stats().
    /// </summary>
    /// <typeparam name="TSample">The sample type.</typeparam>
    /// <typeparam name="TOperator">The texture operator of the sample type.</typeparam>
    /// <param name="luma">The luma plane of the frame source.</param>
    /// <param name="bitDepth">The luma bit depth.</param>
    /// <param name="qIndex">The frame quantizer index.</param>
    /// <param name="orderHint">The order hint of the frame.</param>
    public void CompleteFrame<TSample, TOperator>(Av1PlaneRegion<TSample> luma, int bitDepth, int qIndex, int orderHint)
        where TSample : unmanaged
        where TOperator : struct, ITextureOperator<TSample>
    {
        int shift = bitDepth - 8;
        ReadOnlySpan<TSample> lumaSamples = luma.Samples;
        foreach (TextureBlock block in this.textureBlocks)
        {
            AccumulateTexture<TSample, TOperator>(
                luma,
                lumaSamples,
                block.Origin,
                block.Size.GetWidth(),
                block.Size.GetHeight(),
                shift,
                ref this.horizontalTexture,
                ref this.verticalTexture,
                ref this.diagonalTexture);
        }

        this.textureBlocks.Clear();
        this.q = qIndex;
        this.order = orderHint;
        this.valid = true;
    }

    /// <summary>
    /// Evaluates the network over the normalized statistics with the float arithmetic order of the x64 reference
    /// build, and rounds the score to nine fractional bits. Reference: get_smart_mv_prec() with av1_nn_predict_avx2()
    /// and av1_nn_output_prec_reduce().
    /// </summary>
    private bool GetSmartPrecision(int qIndex, int orderHint, Size frameSize)
    {
        float area = frameSize.Width * frameSize.Height;
        Span<float> features = stackalloc float[FeatureCount];
        features[0] = qIndex;
        features[1] = this.q;
        features[2] = orderHint - this.order;
        features[3] = this.interCount / area;
        features[4] = this.intraCount / area;
        features[5] = this.defaultMotionVectors / area;
        features[6] = this.jointCounts[0] / area;
        features[7] = this.jointCounts[1] / area;
        features[8] = this.jointCounts[2] / area;
        features[9] = this.jointCounts[3] / area;
        features[10] = this.lastBitZero / area;
        features[11] = this.lastBitNonzero / area;
        features[12] = this.totalRate / area;
        features[13] = this.highPrecisionTotalRate / area;
        features[14] = this.lowPrecisionTotalRate / area;
        features[15] = this.horizontalTexture / area;
        features[16] = this.verticalTexture / area;
        features[17] = this.diagonalTexture / area;

        ReadOnlySpan<float> means = FeatureMeans;
        ReadOnlySpan<float> deviations = FeatureDeviations;
        for (int feature = 0; feature < FeatureCount; feature++)
        {
            features[feature] = (features[feature] - means[feature]) / deviations[feature];
        }

        // The first sixteen inputs of each hidden node go through the eight-by-eight propagation: in each group of
        // eight, adjacent products add, then the pairs, then the two halves; the second group adds onto the first,
        // then the bias. The two remaining inputs add one at a time before the node is clipped.
        ReadOnlySpan<float> hiddenWeights = HiddenWeights;
        ReadOnlySpan<float> hiddenBiases = HiddenBiases;
        Span<float> hidden = stackalloc float[HiddenCount];
        for (int node = 0; node < HiddenCount; node++)
        {
            ReadOnlySpan<float> weights = hiddenWeights.Slice(node * FeatureCount, FeatureCount);
            float value = (SumEightProducts(features, weights, 0) + SumEightProducts(features, weights, 8)) + hiddenBiases[node];
            value += features[16] * weights[16];
            value += features[17] * weights[17];
            hidden[node] = Math.Max(value, 0f);
        }

        // The output accumulates the hidden nodes in eight lanes, lane j holding nodes j, j + 8, j + 16 and j + 24.
        // The high half adds onto the low half, then adjacent pairs, then the two pair sums.
        ReadOnlySpan<float> outputWeights = OutputWeights;
        Span<float> lanes = stackalloc float[8];
        lanes.Clear();
        for (int chunk = 0; chunk < HiddenCount; chunk += 8)
        {
            for (int lane = 0; lane < 8; lane++)
            {
                lanes[lane] += hidden[chunk + lane] * outputWeights[chunk + lane];
            }
        }

        float sum0 = lanes[0] + lanes[4];
        float sum1 = lanes[1] + lanes[5];
        float sum2 = lanes[2] + lanes[6];
        float sum3 = lanes[3] + lanes[7];
        const float OutputBias = -0.341771735378258f;
        float score = OutputBias + ((sum2 + sum3) + (sum0 + sum1));

        // The float product widens to double before the half is added, and the truncated integer multiplies the float
        // reciprocal.
        const int Precision = 1 << 9;
        const float InversePrecision = (float)(1.0 / Precision);
        score = (int)((double)(score * Precision) + 0.5) * InversePrecision;
        return score >= 0f;
    }

    /// <summary>
    /// Sums eight products of one input group in the pairwise order of the horizontal additions.
    /// </summary>
    private static float SumEightProducts(ReadOnlySpan<float> inputs, ReadOnlySpan<float> weights, int start)
    {
        float p0 = inputs[start] * weights[start];
        float p1 = inputs[start + 1] * weights[start + 1];
        float p2 = inputs[start + 2] * weights[start + 2];
        float p3 = inputs[start + 3] * weights[start + 3];
        float p4 = inputs[start + 4] * weights[start + 4];
        float p5 = inputs[start + 5] * weights[start + 5];
        float p6 = inputs[start + 6] * weights[start + 6];
        float p7 = inputs[start + 7] * weights[start + 7];
        return ((p0 + p1) + (p2 + p3)) + ((p4 + p5) + (p6 + p7));
    }

    /// <summary>
    /// Adds the rates and counts of one new vector against its reference, pricing it at the frame precision and at the
    /// precisions without and with the eighth-sample bit. Reference: keep_one_mv_stat().
    /// </summary>
    private void KeepMotionVector(Av1MotionVector reference, Av1MotionVector vector)
    {
        int row = vector.Row - reference.Row;
        int column = vector.Column - reference.Column;
        int joint = GetJoint(row, column);

        // The eighth-sample difference is the coded one; the quarter-sample difference truncates each component to
        // even units when the frame codes eighth-sample vectors.
        int lowRow = this.useHighPrecision ? (row / 2) * 2 : row;
        int lowColumn = this.useHighPrecision ? (column / 2) * 2 : column;
        int lowJoint = GetJoint(lowRow, lowColumn);

        Av1Distribution jointDistribution = this.context.Joint;
        int jointRate = Av1ProbabilityCost.GetSymbolCost(jointDistribution, joint);
        int lowJointRate = Av1ProbabilityCost.GetSymbolCost(jointDistribution, lowJoint);
        jointDistribution.Update(joint);

        this.totalRate += jointRate;
        this.highPrecisionTotalRate += jointRate;
        this.lowPrecisionTotalRate += lowJointRate;
        this.jointCounts[joint]++;

        this.KeepComponent(row, lowRow, this.context.Vertical);
        this.KeepComponent(column, lowColumn, this.context.Horizontal);
    }

    /// <summary>
    /// Adds the rates of one vector component. The quarter-sample rate leaves out the eighth-sample bit of the coded
    /// component and is zero when truncation empties the component. Reference: the component loop of
    /// keep_one_mv_stat().
    /// </summary>
    private void KeepComponent(int value, int lowValue, Av1MotionVectorContext.Component component)
    {
        if (value == 0)
        {
            return;
        }

        int rate = this.KeepComponentRates(value, component, out int highPrecisionRate);
        this.totalRate += rate;
        this.highPrecisionTotalRate += rate;
        this.lowPrecisionTotalRate += lowValue != 0 ? rate - highPrecisionRate : 0;
    }

    /// <summary>
    /// Prices and adapts the symbols of one nonzero component, and counts its last bit. Reference:
    /// keep_one_comp_stat().
    /// </summary>
    private int KeepComponentRates(int value, Av1MotionVectorContext.Component component, out int highPrecisionRate)
    {
        int sign = value < 0 ? 1 : 0;
        int magnitude = sign != 0 ? -value : value;
        int magnitudeClass = GetMagnitudeClass(magnitude - 1, out int offset);
        int integerPart = offset >> 3;
        int fractionalPart = (offset >> 1) & 3;
        int highPart = offset & 1;

        int signRate = Av1ProbabilityCost.GetSymbolCost(component.Sign, sign);
        component.Sign.Update(sign);

        int classRate = Av1ProbabilityCost.GetSymbolCost(component.MagnitudeClass, magnitudeClass);
        component.MagnitudeClass.Update(magnitudeClass);

        int integerRate = 0;
        if (magnitudeClass == 0)
        {
            integerRate = Av1ProbabilityCost.GetSymbolCost(component.ClassZero, integerPart);
            component.ClassZero.Update(integerPart);
        }
        else
        {
            for (int bit = 0; bit < magnitudeClass; bit++)
            {
                int symbol = (integerPart >> bit) & 1;
                integerRate += Av1ProbabilityCost.GetSymbolCost(component.OffsetBits[bit], symbol);
                component.OffsetBits[bit].Update(symbol);
            }
        }

        Av1Distribution fractionalDistribution = magnitudeClass == 0
            ? component.ClassZeroFractional[integerPart]
            : component.Fractional;

        int fractionalRate = Av1ProbabilityCost.GetSymbolCost(fractionalDistribution, fractionalPart);
        fractionalDistribution.Update(fractionalPart);

        highPrecisionRate = 0;
        if (this.useHighPrecision)
        {
            Av1Distribution highDistribution = magnitudeClass == 0 ? component.ClassZeroHighPrecision : component.HighPrecision;
            highPrecisionRate = Av1ProbabilityCost.GetSymbolCost(highDistribution, highPart);
            highDistribution.Update(highPart);
        }

        this.lastBitZero += highPart ^ 1;
        this.lastBitNonzero += highPart;
        return signRate + classRate + integerRate + fractionalRate + highPrecisionRate;
    }

    /// <summary>
    /// Gets the joint type of a vector difference. Reference: av1_get_mv_joint().
    /// </summary>
    private static int GetJoint(int row, int column) => (row != 0 ? 2 : 0) | (column != 0 ? 1 : 0);

    /// <summary>
    /// Gets the magnitude class of a component magnitude less one, and its offset from the class base. Reference:
    /// av1_get_mv_class().
    /// </summary>
    private static int GetMagnitudeClass(int value, out int offset)
    {
        const int MaximumClass = 10;
        int wholeSamples = value >> 3;
        int magnitudeClass = wholeSamples == 0 ? 0 : Math.Min(Av1Math.MostSignificantBit((uint)wholeSamples), MaximumClass);
        offset = value - (magnitudeClass == 0 ? 0 : 2 << (magnitudeClass + 2));
        return magnitudeClass;
    }

    /// <summary>
    /// An inter block whose texture the frame completion measures.
    /// </summary>
    private readonly struct TextureBlock
    {
        public TextureBlock(Point origin, Av1BlockSize size)
        {
            this.Origin = origin;
            this.Size = size;
        }

        public Point Origin { get; }

        public Av1BlockSize Size { get; }
    }
}
