// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Contains a list of chunk types.
/// </summary>
internal enum PngChunkType : uint
{
    /// <summary>
    /// </summary>
    /// <remarks>acTL</remarks>
    AnimationControl = 0x6163544cU,

    /// <summary>
    /// </summary>
    /// <remarks>fcTL</remarks>
    FrameControl = 0x6663544cU,

    /// <summary>
    /// </summary>
    /// <remarks>fdAT</remarks>
    FrameData = 0x66644154U,

    /// <summary>
    /// The IDAT chunk contains the actual image data. The image can contains more
    /// than one chunk of this type. All chunks together are the whole image.
    /// </summary>
    /// <remarks>IDAT</remarks>
    Data = 0x49444154U,

    /// <summary>
    /// This chunk must appear last. It marks the end of the PNG data stream.
    /// The chunk's data field is empty.
    /// </summary>
    /// <remarks>IEND</remarks>
    End = 0x49454E44U,

    /// <summary>
    /// The first chunk in a png file. Can only exists once. Contains
    /// common information like the width and the height of the image or
    /// the used compression method.
    /// </summary>
    /// <remarks>IHDR</remarks>
    Header = 0x49484452U,

    /// <summary>
    /// The PLTE chunk contains from 1 to 256 palette entries, each a three byte
    /// series in the RGB format.
    /// </summary>
    /// <remarks>PLTE</remarks>
    Palette = 0x504C5445U,

    /// <summary>
    /// The eXIf data chunk which contains the Exif profile.
    /// </summary>
    /// <remarks>eXIF</remarks>
    Exif = 0x65584966U,

    /// <summary>
    /// This chunk specifies the relationship between the image samples and the desired
    /// display output intensity.
    /// </summary>
    /// <remarks>gAMA</remarks>
    Gamma = 0x67414D41U,

    /// <summary>
    /// This chunk specifies the intended pixel size or aspect ratio for display of the image.
    /// </summary>
    /// <remarks>pHYs</remarks>
    Physical = 0x70485973U,

    /// <summary>
    /// Textual information that the encoder wishes to record with the image can be stored in
    /// tEXt chunks. Each tEXt chunk contains a keyword and a text string.
    /// </summary>
    /// <remarks>tEXT</remarks>
    Text = 0x74455874U,

    /// <summary>
    /// Textual information that the encoder wishes to record with the image. The zTXt and tEXt chunks are semantically equivalent,
    /// but the zTXt chunk is recommended for storing large blocks of text. Each zTXt chunk contains a (uncompressed) keyword and
    /// a compressed text string.
    /// </summary>
    /// <remarks>zTXt</remarks>
    CompressedText = 0x7A545874U,

    /// <summary>
    /// This chunk contains International textual data. It contains a keyword, an optional language tag, an optional translated keyword
    /// and the actual text string, which can be compressed or uncompressed.
    /// </summary>
    /// <remarks>iTXt</remarks>
    InternationalText = 0x69545874U,

    /// <summary>
    /// This chunk specifies that the image uses simple transparency:
    /// either alpha values associated with palette entries (for indexed-color images)
    /// or a single transparent color (for grayscale and true color images).
    /// </summary>
    /// <remarks>tRNS</remarks>
    Transparency = 0x74524E53U,

    /// <summary>
    /// This chunk gives the time of the last image modification (not the time of initial image creation).
    /// </summary>
    /// <remarks>tIME</remarks>
    Time = 0x74494d45,

    /// <summary>
    /// This chunk specifies a default background colour to present the image against.
    /// If there is any other preferred background, either user-specified or part of a larger page (as in a browser),
    /// the bKGD chunk should be ignored.
    /// </summary>
    /// <remarks>bKGD</remarks>
    Background = 0x624b4744,

    /// <summary>
    /// This chunk contains a embedded color profile. If the iCCP chunk is present,
    /// the image samples conform to the colour space represented by the embedded ICC profile as defined by the International Color Consortium.
    /// </summary>
    /// <remarks>iCCP</remarks>
    EmbeddedColorProfile = 0x69434350,

    /// <summary>
    /// This chunk defines the original number of significant bits (which can be less than or equal to the sample depth).
    /// This allows PNG decoders to recover the original data losslessly even if the data had a sample depth not directly supported by PNG.
    /// </summary>
    /// <remarks>sBIT</remarks>
    SignificantBits = 0x73424954,

    /// <summary>
    /// If the this chunk is present, the image samples conform to the sRGB colour space [IEC 61966-2-1] and should be displayed
    /// using the specified rendering intent defined by the International Color Consortium.
    /// </summary>
    /// <remarks>sRGB</remarks>
    StandardRgbColourSpace = 0x73524742,

    /// <summary>
    /// This chunk gives the approximate usage frequency of each colour in the palette.
    /// </summary>
    /// <remarks>hIST</remarks>
    Histogram = 0x68495354,

    /// <summary>
    /// This chunk contains the suggested palette.
    /// </summary>
    /// <remarks>sPLT</remarks>
    SuggestedPalette = 0x73504c54,

    /// <summary>
    /// This chunk may be used to specify the 1931 CIE x,y chromaticities of the red,
    /// green, and blue display primaries used in the image, and the referenced white point.
    /// </summary>
    /// <remarks>cHRM</remarks>
    Chroma = 0x6348524d,

    /// <summary>
    /// Malformed chunk named CgBI produced by apple, which is not conform to the specification.
    /// Related issue is here https://github.com/SixLabors/ImageSharp/issues/410
    /// </summary>
    /// <remarks>CgBI</remarks>
    ProprietaryApple = 0x43674249
}
