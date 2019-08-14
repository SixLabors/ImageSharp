# ImageSharp TIFF codec

## References
- TIFF
  - [TIFF 6.0 Specification](http://partners.adobe.com/public/developer/en/tiff/TIFF6.pdf),(http://www.npes.org/pdf/TIFF-v6.pdf)
  - [TIFF Supplement 1](http://partners.adobe.com/public/developer/en/tiff/TIFFPM6.pdf)
  - [TIFF Supplement 2](http://partners.adobe.com/public/developer/en/tiff/TIFFphotoshop.pdf)
  - [TIFF Supplement 3](http://chriscox.org/TIFFTN3d1.pdf)
  - [TIFF-F/FX Extension (RFC2301)](http://www.ietf.org/rfc/rfc2301.txt)
  - [TIFF/EP Extension (Wikipedia)](https://en.wikipedia.org/wiki/TIFF/EP)
  - [Adobe TIFF Pages](http://partners.adobe.com/public/developer/tiff/index.html)
  - [Unofficial TIFF FAQ](http://www.awaresystems.be/imaging/tiff/faq.html)

- DNG
  - [Adobe DNG Pages](https://helpx.adobe.com/photoshop/digital-negative.html)

- Metadata (EXIF)
  - [EXIF 2.3 Specification](http://www.cipa.jp/std/documents/e/DC-008-2012_E.pdf)

- Metadata (XMP)
  - [Adobe XMP Pages](http://www.adobe.com/products/xmp.html)
  - [Adobe XMP Developer Center](http://www.adobe.com/devnet/xmp.html)

## Implementation Status

### Deviations from the TIFF spec (to be fixed)

- Decoder
  - A Baseline TIFF reader must skip over extra components (e.g. RGB with 4 samples per pixels)
    - NB: Need to handle this for both planar and chunky data
  - If the SampleFormat field is present and not 1 - fail gracefully if you cannot handle this
  - Compression=None should treat 16/32-BitsPerSample for all samples as SHORT/LONG (for byte order and padding rows)
  - RowsPerStrip should default to 2^32-1 (effectively infinity) to store the image as a single strip
  - Check Planar format data - is this encoded as strips in order RGBRGBRGB or RRRGGGBBB?
  - Make sure we ignore any strips that are not needed for the image (if too many are present)

### Compression Formats

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|None                       |       |   Y   |                          |
|Ccitt1D                    |       |       |                          |
|PackBits                   |       |   Y   |                          |
|CcittGroup3Fax             |       |       |                          |
|CcittGroup4Fax             |       |       |                          |
|Lzw                        |       |   Y   | Based on ImageSharp GIF LZW implementation - this code could be modified to be (i) shared, or (ii) optimised for each case |
|Old Jpeg                   |       |       |                          |
|Jpeg (Technote 2)          |       |       |                          |
|Deflate (Technote 2)       |       |   Y   |                          |
|Old Deflate (Technote 2)   |       |   Y   |                          |

### Photometric Interpretation Formats

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|WhiteIsZero                |       |   Y   | General + 1/4/8-bit optimised implementations |
|BlackIsZero                |       |   Y   | General + 1/4/8-bit optimised implementations |
|Rgb (Chunky)               |       |   Y   | General + Rgb888 optimised implementation |
|Rgb (Planar)               |       |   Y   | General implementation only |
|PaletteColor               |       |   Y   | General implementation only |
|TransparencyMask           |       |       |                          |
|Separated (TIFF Extension) |       |       |                          |
|YCbCr (TIFF Extension)     |       |       |                          |
|CieLab (TIFF Extension)    |       |       |                          |
|IccLab (TechNote 1)        |       |       |                          |

### Baseline TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|NewSubfileType             |       |       |                          |
|SubfileType                |       |       |                          |
|ImageWidth                 |       |   Y   |                          |
|ImageLength                |       |   Y   |                          |
|BitsPerSample              |       |   Y   |                          |
|Compression                |       |   Y   |                          |
|PhotometricInterpretation  |       |   Y   |                          |
|Threshholding              |       |       |                          |
|CellWidth                  |       |       |                          |
|CellLength                 |       |       |                          |
|FillOrder                  |       |       |                          |
|ImageDescription           |       |   Y   |                          |
|Make                       |       |   Y   |                          |
|Model                      |       |   Y   |                          |
|StripOffsets               |       |   Y   |                          |
|Orientation                |       |       |                          |
|SamplesPerPixel            |       |       | Currently ignored, as can be inferred from count of BitsPerSample |
|RowsPerStrip               |       |   Y   |                          |
|StripByteCounts            |       |   Y   |                          |
|MinSampleValue             |       |       |                          |
|MaxSampleValue             |       |       |                          |
|XResolution                |       |   Y   |                          |
|YResolution                |       |   Y   |                          |
|PlanarConfiguration        |       |   Y   |                          |
|FreeOffsets                |       |       |                          |
|FreeByteCounts             |       |       |                          |
|GrayResponseUnit           |       |       |                          |
|GrayResponseCurve          |       |       |                          |
|ResolutionUnit             |       |   Y   |                          |
|Software                   |       |   Y   |                          |
|DateTime                   |       |   Y   |                          |
|Artist                     |       |   Y   |                          |
|HostComputer               |       |   Y   |                          |
|ColorMap                   |       |   Y   |                          |
|ExtraSamples               |       |       |                          |
|Copyright                  |       |   Y   |                          |

### Extension TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|NewSubfileType             |       |       |                          |
|DocumentName               |       |       |                          |
|PageName                   |       |       |                          |
|XPosition                  |       |       |                          |
|YPosition                  |       |       |                          |
|T4Options                  |       |       |                          |
|T6Options                  |       |       |                          |
|PageNumber                 |       |       |                          |
|TransferFunction           |       |       |                          |
|Predictor                  |       |       |                          |
|WhitePoint                 |       |       |                          |
|PrimaryChromaticities      |       |       |                          |
|HalftoneHints              |       |       |                          |
|TileWidth                  |       |       |                          |
|TileLength                 |       |       |                          |
|TileOffsets                |       |       |                          |
|TileByteCounts             |       |       |                          |
|BadFaxLines                |       |       |                          |
|CleanFaxData               |       |       |                          |
|ConsecutiveBadFaxLines     |       |       |                          |
|SubIFDs                    |       |       |                          |
|InkSet                     |       |       |                          |
|InkNames                   |       |       |                          |
|NumberOfInks               |       |       |                          |
|DotRange                   |       |       |                          |
|TargetPrinter              |       |       |                          |
|SampleFormat               |       |       |                          |
|SMinSampleValue            |       |       |                          |
|SMaxSampleValue            |       |       |                          |
|TransferRange              |       |       |                          |
|ClipPath                   |       |       |                          |
|XClipPathUnits             |       |       |                          |
|YClipPathUnits             |       |       |                          |
|Indexed                    |       |       |                          |
|JPEGTables                 |       |       |                          |
|OPIProxy                   |       |       |                          |
|GlobalParametersIFD        |       |       |                          |
|ProfileType                |       |       |                          |
|FaxProfile                 |       |       |                          |
|CodingMethods              |       |       |                          |
|VersionYear                |       |       |                          |
|ModeNumber                 |       |       |                          |
|Decode                     |       |       |                          |
|DefaultImageColor          |       |       |                          |
|JPEGProc                   |       |       |                          |
|JPEGInterchangeFormat      |       |       |                          |
|JPEGInterchangeFormatLength|       |       |                          |
|JPEGRestartInterval        |       |       |                          |
|JPEGLosslessPredictors     |       |       |                          |
|JPEGPointTransforms        |       |       |                          |
|JPEGQTables                |       |       |                          |
|JPEGDCTables               |       |       |                          |
|JPEGACTables               |       |       |                          |
|YCbCrCoefficients          |       |       |                          |
|YCbCrSubSampling           |       |       |                          |
|YCbCrPositioning           |       |       |                          |
|ReferenceBlackWhite        |       |       |                          |
|StripRowCounts             |       |       |                          |
|XMP                        |       |       |                          |
|ImageID                    |       |       |                          |
|ImageLayer                 |       |       |                          |

### Private TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|Wang Annotation            |       |       |                          |
|MD FileTag                 |       |       |                          |
|MD ScalePixel              |       |       |                          |
|MD ColorTable              |       |       |                          |
|MD LabName                 |       |       |                          |
|MD SampleInfo              |       |       |                          |
|MD PrepDate                |       |       |                          |
|MD PrepTime                |       |       |                          |
|MD FileUnits               |       |       |                          |
|ModelPixelScaleTag         |       |       |                          |
|IPTC                       |       |       |                          |
|INGR Packet Data Tag       |       |       |                          |
|INGR Flag Registers        |       |       |                          |
|IrasB Transformation Matrix|       |       |                          |
|ModelTiepointTag           |       |       |                          |
|ModelTransformationTag     |       |       |                          |
|Photoshop                  |       |       |                          |
|Exif IFD                   |       |       |                          |
|ICC Profile                |       |       |                          |
|GeoKeyDirectoryTag         |       |       |                          |
|GeoDoubleParamsTag         |       |       |                          |
|GeoAsciiParamsTag          |       |       |                          |
|GPS IFD                    |       |       |                          |
|HylaFAX FaxRecvParams      |       |       |                          |
|HylaFAX FaxSubAddress      |       |       |                          |
|HylaFAX FaxRecvTime        |       |       |                          |
|ImageSourceData            |       |       |                          |
|Interoperability IFD       |       |       |                          |
|GDAL_METADATA              |       |       |                          |
|GDAL_NODATA                |       |       |                          |
|Oce Scanjob Description    |       |       |                          |
|Oce Application Selector   |       |       |                          |
|Oce Identification Number  |       |       |                          |
|Oce ImageLogic Characteristics|       |       |                          |
|DNGVersion                 |       |       |                          |
|DNGBackwardVersion         |       |       |                          |
|UniqueCameraModel          |       |       |                          |
|LocalizedCameraModel       |       |       |                          |
|CFAPlaneColor              |       |       |                          |
|CFALayout                  |       |       |                          |
|LinearizationTable         |       |       |                          |
|BlackLevelRepeatDim        |       |       |                          |
|BlackLevel                 |       |       |                          |
|BlackLevelDeltaH           |       |       |                          |
|BlackLevelDeltaV           |       |       |                          |
|WhiteLevel                 |       |       |                          |
|DefaultScale               |       |       |                          |
|DefaultCropOrigin          |       |       |                          |
|DefaultCropSize            |       |       |                          |
|ColorMatrix1               |       |       |                          |
|ColorMatrix2               |       |       |                          |
|CameraCalibration1         |       |       |                          |
|CameraCalibration2         |       |       |                          |
|ReductionMatrix1           |       |       |                          |
|ReductionMatrix2           |       |       |                          |
|AnalogBalance              |       |       |                          |
|AsShotNeutral              |       |       |                          |
|AsShotWhiteXY              |       |       |                          |
|BaselineExposure           |       |       |                          |
|BaselineNoise              |       |       |                          |
|BaselineSharpness          |       |       |                          |
|BayerGreenSplit            |       |       |                          |
|LinearResponseLimit        |       |       |                          |
|CameraSerialNumber         |       |       |                          |
|LensInfo                   |       |       |                          |
|ChromaBlurRadius           |       |       |                          |
|AntiAliasStrength          |       |       |                          |
|DNGPrivateData             |       |       |                          |
|MakerNoteSafety            |       |       |                          |
|CalibrationIlluminant1     |       |       |                          |
|CalibrationIlluminant2     |       |       |                          |
|BestQualityScale           |       |       |                          |
|Alias Layer Metadata       |       |       |                          |
