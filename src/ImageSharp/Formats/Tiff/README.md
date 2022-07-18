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
  - [CCITT T.4 Compression](https://www.itu.int/rec/T-REC-T.4-198811-S/_page.print)
  - [CCITT T.6 Compression](https://www.itu.int/rec/T-REC-T.6/en)

- DNG
  - [Adobe DNG Pages](https://helpx.adobe.com/photoshop/digital-negative.html)

- Metadata (EXIF)
  - [EXIF 2.3 Specification](http://www.cipa.jp/std/documents/e/DC-008-2012_E.pdf)

- Metadata (XMP)
  - [Adobe XMP Pages](http://www.adobe.com/products/xmp.html)
  - [Adobe XMP Developer Center](http://www.adobe.com/devnet/xmp.html)

## Implementation Status

- The Decoder currently only supports decoding multiframe images, which have the same dimensions.
- Some compression formats are not yet supported. See the list below.

### Compression Formats

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|None                       |   Y   |   Y   |                          |
|Ccitt1D                    |   Y   |   Y   |                          |
|PackBits                   |   Y   |   Y   |                          |
|CcittGroup3Fax             |   Y   |   Y   |                          |
|CcittGroup4Fax             |   Y   |   Y   |                          |
|Lzw                        |   Y   |   Y   | Based on ImageSharp GIF LZW implementation - this code could be modified to be (i) shared, or (ii) optimised for each case. |
|Old Jpeg                   |       |       | We should not even try to support this. |
|Jpeg (Technote 2)          |   Y   |   Y   |                          |
|Deflate (Technote 2)       |   Y   |   Y   | Based on PNG Deflate.    |
|Old Deflate (Technote 2)   |       |   Y   |                          |
|Webp   					|       |   Y   |                          |

### Photometric Interpretation Formats

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|WhiteIsZero                |   Y   |   Y   | General + 1/4/8-bit optimised implementations |
|BlackIsZero                |   Y   |   Y   | General + 1/4/8-bit optimised implementations |
|Rgb (Chunky)               |   Y   |   Y   | General + Rgb888 optimised implementation |
|Rgb (Planar)               |       |   Y   | General implementation only |
|PaletteColor               |   Y   |   Y   | General implementation only |
|TransparencyMask           |       |       |                          |
|Separated (TIFF Extension) |       |       |                          |
|YCbCr (TIFF Extension)     |       |   Y   |                          |
|CieLab (TIFF Extension)    |       |   Y   |                          |
|IccLab (TechNote 1)        |       |       |                          |

### Baseline TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|NewSubfileType             |       |       |                          |
|SubfileType                |       |       |                          |
|ImageWidth                 |   Y   |   Y   |                          |
|ImageLength                |   Y   |   Y   |                          |
|BitsPerSample              |   Y   |   Y   |                          |
|Compression                |   Y   |   Y   |                          |
|PhotometricInterpretation  |   Y   |   Y   |                          |
|Thresholding               |       |       |                          |
|CellWidth                  |       |       |                          |
|CellLength                 |       |       |                          |
|FillOrder                  |       |   Y   | 						   |
|ImageDescription           |   Y   |   Y   |                          |
|Make                       |   Y   |   Y   |                          |
|Model                      |   Y   |   Y   |                          |
|StripOffsets               |   Y   |   Y   |                          |
|Orientation                |       |   -   | Ignore. Many readers ignore this tag. |
|SamplesPerPixel            |   Y   |   -   | Currently ignored, as can be inferred from count of BitsPerSample. |
|RowsPerStrip               |   Y   |   Y   |                          |
|StripByteCounts            |   Y   |   Y   |                          |
|MinSampleValue             |       |       |                          |
|MaxSampleValue             |       |       |                          |
|XResolution                |   Y   |   Y   |                          |
|YResolution                |   Y   |   Y   |                          |
|PlanarConfiguration        |       |   Y   | Encoding support only chunky. |
|FreeOffsets                |       |       |                          |
|FreeByteCounts             |       |       |                          |
|GrayResponseUnit           |       |       |                          |
|GrayResponseCurve          |       |       |                          |
|ResolutionUnit             |   Y   |   Y   |                          |
|Software                   |   Y   |   Y   |                          |
|DateTime                   |   Y   |   Y   |                          |
|Artist                     |   Y   |   Y   |                          |
|HostComputer               |   Y   |   Y   |                          |
|ColorMap                   |   Y   |   Y   |                          |
|ExtraSamples               |       |   Y   | Unspecified alpha data is not supported. |
|Copyright                  |   Y   |   Y   |                          |

### Extension TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|NewSubfileType             |       |       |                          |
|DocumentName               |   Y   |   Y   |                          |
|PageName                   |       |       |                          |
|XPosition                  |       |       |                          |
|YPosition                  |       |       |                          |
|T4Options                  |       |   Y   |                          |
|T6Options                  |       |       |                          |
|PageNumber                 |       |       |                          |
|TransferFunction           |       |       |                          |
|Predictor                  |   Y   |   Y   | only Horizontal          |
|WhitePoint                 |       |       |                          |
|PrimaryChromaticities      |       |       |                          |
|HalftoneHints              |       |       |                          |
|TileWidth                  |       |   -   |                          |
|TileLength                 |       |   -   |                          |
|TileOffsets                |       |   -   |                          |
|TileByteCounts             |       |   -   |                          |
|BadFaxLines                |       |       |                          |
|CleanFaxData               |       |       |                          |
|ConsecutiveBadFaxLines     |       |       |                          |
|SubIFDs                    |       |   -   |                          |
|InkSet                     |       |       |                          |
|InkNames                   |       |       |                          |
|NumberOfInks               |       |       |                          |
|DotRange                   |       |       |                          |
|TargetPrinter              |       |       |                          |
|SampleFormat               |       |   -   |                          |
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
|YCbCrCoefficients          |       |   Y   |                          |
|YCbCrSubSampling           |       |   Y   |                          |
|YCbCrPositioning           |       |       |                          |
|ReferenceBlackWhite        |       |   Y   |                          |
|StripRowCounts             |   -   |   -   | See RFC 2301 (File Format for Internet Fax). |
|XMP                        |   Y   |   Y   |                          |
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
|IPTC                       |   Y   |   Y   |                          |
|INGR Packet Data Tag       |       |       |                          |
|INGR Flag Registers        |       |       |                          |
|IrasB Transformation Matrix|       |       |                          |
|ModelTiepointTag           |       |       |                          |
|ModelTransformationTag     |       |       |                          |
|Photoshop                  |       |       |                          |
|Exif IFD                   |       |   -   | 0x8769 SubExif           |
|ICC Profile                |   Y   |   Y   |                          |
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
