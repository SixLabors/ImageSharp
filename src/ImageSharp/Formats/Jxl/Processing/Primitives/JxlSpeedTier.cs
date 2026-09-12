// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Defines how quickly or slowly to encode an image. Slower
/// modes may take longer to encode the image but provide
/// better compression, while faster modes skip many algorithms,
/// therefore making compression faster, but at the same time,
/// less efficient.
/// </summary>
internal enum JxlSpeedTier : sbyte
{
    /// <summary>
    /// 🌍 Try multiple combinations of Glacier
    /// flags for modular mode. Otherwise like Glacier.
    /// </summary>
    TectonicPlate = -1,

    /// <summary>
    /// 🧊 Learn a global tree in Modular mode.
    /// </summary>
    Glacier,

    /// <summary>
    /// 🐢 Turns on FindBestQuantizationHQ loop.
    /// </summary>
    Tortoise,

    /// <summary>
    /// 🐈 Turns on FindBestQuantization butteraugli loop.
    /// </summary>
    Kitten,

    /// <summary>
    /// 🐿️ Turns on dots, patches, and spline detection, as well as
    /// context clustering. This is the default mode.
    /// </summary>
    Squirrel,

    /// <summary>
    /// 🐻 Turns on error diffusion and full AC strategy heuristics. This is the
    /// equivalent of fast mode.
    /// </summary>
    Wombat,

    /// <summary>
    /// 🐰 Turns on simple heuristics for AC strategy, quant field,
    /// gaborish by default, non-default color map, initial quant field,
    /// and non-default Chroma from Luma.
    /// </summary>
    Hare,

    /// <summary>
    /// 🐆 Turns on clustering and enables coefficient reordering.
    /// </summary>
    Cheetah,

    /// <summary>
    /// 🦅 Turns off most encoder encoder features. Does context clustering.
    /// For modular, uses fixed tree with Weighted predictor.
    /// </summary>
    Falcon,

    /// <summary>
    /// ⚡ Fastest possible setting for VarDCT. For Modular, uses fixed tree with
    /// Gradient predictor.
    /// </summary>
    Thunder,

    /// <summary>
    /// ⚡ For VarDCT, same as Thunder. For Modular, no tree, Gradient predictor, fast histograms.
    /// </summary>
    Lightning
}
