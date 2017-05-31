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
  - [Adobe XMP Developer Centre](http://www.adobe.com/devnet/xmp.html)

## Implementation Status

### Baseline TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|NewSubfileType             |  [ ]  |  [ ]  |                          |
|SubfileType                |  [ ]  |  [ ]  |                          |
|ImageWidth                 |  [ ]  |  [ ]  |                          |
|ImageLength                |  [ ]  |  [ ]  |                          |
|BitsPerSample              |  [ ]  |  [ ]  |                          |
|Compression                |  [ ]  |  [ ]  |                          |
|PhotometricInterpretation  |  [ ]  |  [ ]  |                          |
|Threshholding              |  [ ]  |  [ ]  |                          |
|CellWidth                  |  [ ]  |  [ ]  |                          |
|CellLength                 |  [ ]  |  [ ]  |                          |
|FillOrder                  |  [ ]  |  [ ]  |                          |
|ImageDescription           |  [ ]  |  [ ]  |                          |
|Make                       |  [ ]  |  [ ]  |                          |
|Model                      |  [ ]  |  [ ]  |                          |
|StripOffsets               |  [ ]  |  [ ]  |                          |
|Orientation                |  [ ]  |  [ ]  |                          |
|SamplesPerPixel            |  [ ]  |  [ ]  |                          |
|RowsPerStrip               |  [ ]  |  [ ]  |                          |
|StripByteCounts            |  [ ]  |  [ ]  |                          |
|MinSampleValue             |  [ ]  |  [ ]  |                          |
|MaxSampleValue             |  [ ]  |  [ ]  |                          |
|XResolution                |  [ ]  |  [ ]  |                          |
|YResolution                |  [ ]  |  [ ]  |                          |
|PlanarConfiguration        |  [ ]  |  [ ]  |                          |
|FreeOffsets                |  [ ]  |  [ ]  |                          |
|FreeByteCounts             |  [ ]  |  [ ]  |                          |
|GrayResponseUnit           |  [ ]  |  [ ]  |                          |
|GrayResponseCurve          |  [ ]  |  [ ]  |                          |
|ResolutionUnit             |  [ ]  |  [ ]  |                          |
|Software                   |  [ ]  |  [ ]  |                          |
|DateTime                   |  [ ]  |  [ ]  |                          |
|Artist                     |  [ ]  |  [ ]  |                          |
|HostComputer               |  [ ]  |  [ ]  |                          |
|ColorMap                   |  [ ]  |  [ ]  |                          |
|ExtraSamples               |  [ ]  |  [ ]  |                          |
|Copyright                  |  [ ]  |  [ ]  |                          |

### Extension TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|NewSubfileType             |  [ ]  |  [ ]  |                          |
|DocumentName               |  [ ]  |  [ ]  |                          |
|PageName                   |  [ ]  |  [ ]  |                          |
|XPosition                  |  [ ]  |  [ ]  |                          |
|YPosition                  |  [ ]  |  [ ]  |                          |
|T4Options                  |  [ ]  |  [ ]  |                          |
|T6Options                  |  [ ]  |  [ ]  |                          |
|PageNumber                 |  [ ]  |  [ ]  |                          |
|TransferFunction           |  [ ]  |  [ ]  |                          |
|Predictor                  |  [ ]  |  [ ]  |                          |
|WhitePoint                 |  [ ]  |  [ ]  |                          |
|PrimaryChromaticities      |  [ ]  |  [ ]  |                          |
|HalftoneHints              |  [ ]  |  [ ]  |                          |
|TileWidth                  |  [ ]  |  [ ]  |                          |
|TileLength                 |  [ ]  |  [ ]  |                          |
|TileOffsets                |  [ ]  |  [ ]  |                          |
|TileByteCounts             |  [ ]  |  [ ]  |                          |
|BadFaxLines                |  [ ]  |  [ ]  |                          |
|CleanFaxData               |  [ ]  |  [ ]  |                          |
|ConsecutiveBadFaxLines     |  [ ]  |  [ ]  |                          |
|SubIFDs                    |  [ ]  |  [ ]  |                          |
|InkSet                     |  [ ]  |  [ ]  |                          |
|InkNames                   |  [ ]  |  [ ]  |                          |
|NumberOfInks               |  [ ]  |  [ ]  |                          |
|DotRange                   |  [ ]  |  [ ]  |                          |
|TargetPrinter              |  [ ]  |  [ ]  |                          |
|SampleFormat               |  [ ]  |  [ ]  |                          |
|SMinSampleValue            |  [ ]  |  [ ]  |                          |
|SMaxSampleValue            |  [ ]  |  [ ]  |                          |
|TransferRange              |  [ ]  |  [ ]  |                          |
|ClipPath                   |  [ ]  |  [ ]  |                          |
|XClipPathUnits             |  [ ]  |  [ ]  |                          |
|YClipPathUnits             |  [ ]  |  [ ]  |                          |
|Indexed                    |  [ ]  |  [ ]  |                          |
|JPEGTables                 |  [ ]  |  [ ]  |                          |
|OPIProxy                   |  [ ]  |  [ ]  |                          |
|GlobalParametersIFD        |  [ ]  |  [ ]  |                          |
|ProfileType                |  [ ]  |  [ ]  |                          |
|FaxProfile                 |  [ ]  |  [ ]  |                          |
|CodingMethods              |  [ ]  |  [ ]  |                          |
|VersionYear                |  [ ]  |  [ ]  |                          |
|ModeNumber                 |  [ ]  |  [ ]  |                          |
|Decode                     |  [ ]  |  [ ]  |                          |
|DefaultImageColor          |  [ ]  |  [ ]  |                          |
|JPEGProc                   |  [ ]  |  [ ]  |                          |
|JPEGInterchangeFormat      |  [ ]  |  [ ]  |                          |
|JPEGInterchangeFormatLength|  [ ]  |  [ ]  |                          |
|JPEGRestartInterval        |  [ ]  |  [ ]  |                          |
|JPEGLosslessPredictors     |  [ ]  |  [ ]  |                          |
|JPEGPointTransforms        |  [ ]  |  [ ]  |                          |
|JPEGQTables                |  [ ]  |  [ ]  |                          |
|JPEGDCTables               |  [ ]  |  [ ]  |                          |
|JPEGACTables               |  [ ]  |  [ ]  |                          |
|YCbCrCoefficients          |  [ ]  |  [ ]  |                          |
|YCbCrSubSampling           |  [ ]  |  [ ]  |                          |
|YCbCrPositioning           |  [ ]  |  [ ]  |                          |
|ReferenceBlackWhite        |  [ ]  |  [ ]  |                          |
|StripRowCounts             |  [ ]  |  [ ]  |                          |
|XMP                        |  [ ]  |  [ ]  |                          |
|ImageID                    |  [ ]  |  [ ]  |                          |
|ImageLayer                 |  [ ]  |  [ ]  |                          |

### Private TIFF Tags

|                           |Encoder|Decoder|Comments                  |
|---------------------------|:-----:|:-----:|--------------------------|
|Wang Annotation            |  [ ]  |  [ ]  |                          |
|MD FileTag                 |  [ ]  |  [ ]  |                          |
|MD ScalePixel              |  [ ]  |  [ ]  |                          |
|MD ColorTable              |  [ ]  |  [ ]  |                          |
|MD LabName                 |  [ ]  |  [ ]  |                          |
|MD SampleInfo              |  [ ]  |  [ ]  |                          |
|MD PrepDate                |  [ ]  |  [ ]  |                          |
|MD PrepTime                |  [ ]  |  [ ]  |                          |
|MD FileUnits               |  [ ]  |  [ ]  |                          |
|ModelPixelScaleTag         |  [ ]  |  [ ]  |                          |
|IPTC                       |  [ ]  |  [ ]  |                          |
|INGR Packet Data Tag       |  [ ]  |  [ ]  |                          |
|INGR Flag Registers        |  [ ]  |  [ ]  |                          |
|IrasB Transformation Matrix|  [ ]  |  [ ]  |                          |
|ModelTiepointTag           |  [ ]  |  [ ]  |                          |
|ModelTransformationTag     |  [ ]  |  [ ]  |                          |
|Photoshop                  |  [ ]  |  [ ]  |                          |
|Exif IFD                   |  [ ]  |  [ ]  |                          |
|ICC Profile                |  [ ]  |  [ ]  |                          |
|GeoKeyDirectoryTag         |  [ ]  |  [ ]  |                          |
|GeoDoubleParamsTag         |  [ ]  |  [ ]  |                          |
|GeoAsciiParamsTag          |  [ ]  |  [ ]  |                          |
|GPS IFD                    |  [ ]  |  [ ]  |                          |
|HylaFAX FaxRecvParams      |  [ ]  |  [ ]  |                          |
|HylaFAX FaxSubAddress      |  [ ]  |  [ ]  |                          |
|HylaFAX FaxRecvTime        |  [ ]  |  [ ]  |                          |
|ImageSourceData            |  [ ]  |  [ ]  |                          |
|Interoperability IFD       |  [ ]  |  [ ]  |                          |
|GDAL_METADATA              |  [ ]  |  [ ]  |                          |
|GDAL_NODATA                |  [ ]  |  [ ]  |                          |
|Oce Scanjob Description    |  [ ]  |  [ ]  |                          |
|Oce Application Selector   |  [ ]  |  [ ]  |                          |
|Oce Identification Number  |  [ ]  |  [ ]  |                          |
|Oce ImageLogic Characteristics|  [ ]  |  [ ]  |                          |
|DNGVersion                 |  [ ]  |  [ ]  |                          |
|DNGBackwardVersion         |  [ ]  |  [ ]  |                          |
|UniqueCameraModel          |  [ ]  |  [ ]  |                          |
|LocalizedCameraModel       |  [ ]  |  [ ]  |                          |
|CFAPlaneColor              |  [ ]  |  [ ]  |                          |
|CFALayout                  |  [ ]  |  [ ]  |                          |
|LinearizationTable         |  [ ]  |  [ ]  |                          |
|BlackLevelRepeatDim        |  [ ]  |  [ ]  |                          |
|BlackLevel                 |  [ ]  |  [ ]  |                          |
|BlackLevelDeltaH           |  [ ]  |  [ ]  |                          |
|BlackLevelDeltaV           |  [ ]  |  [ ]  |                          |
|WhiteLevel                 |  [ ]  |  [ ]  |                          |
|DefaultScale               |  [ ]  |  [ ]  |                          |
|DefaultCropOrigin          |  [ ]  |  [ ]  |                          |
|DefaultCropSize            |  [ ]  |  [ ]  |                          |
|ColorMatrix1               |  [ ]  |  [ ]  |                          |
|ColorMatrix2               |  [ ]  |  [ ]  |                          |
|CameraCalibration1         |  [ ]  |  [ ]  |                          |
|CameraCalibration2         |  [ ]  |  [ ]  |                          |
|ReductionMatrix1           |  [ ]  |  [ ]  |                          |
|ReductionMatrix2           |  [ ]  |  [ ]  |                          |
|AnalogBalance              |  [ ]  |  [ ]  |                          |
|AsShotNeutral              |  [ ]  |  [ ]  |                          |
|AsShotWhiteXY              |  [ ]  |  [ ]  |                          |
|BaselineExposure           |  [ ]  |  [ ]  |                          |
|BaselineNoise              |  [ ]  |  [ ]  |                          |
|BaselineSharpness          |  [ ]  |  [ ]  |                          |
|BayerGreenSplit            |  [ ]  |  [ ]  |                          |
|LinearResponseLimit        |  [ ]  |  [ ]  |                          |
|CameraSerialNumber         |  [ ]  |  [ ]  |                          |
|LensInfo                   |  [ ]  |  [ ]  |                          |
|ChromaBlurRadius           |  [ ]  |  [ ]  |                          |
|AntiAliasStrength          |  [ ]  |  [ ]  |                          |
|DNGPrivateData             |  [ ]  |  [ ]  |                          |
|MakerNoteSafety            |  [ ]  |  [ ]  |                          |
|CalibrationIlluminant1     |  [ ]  |  [ ]  |                          |
|CalibrationIlluminant2     |  [ ]  |  [ ]  |                          |
|BestQualityScale           |  [ ]  |  [ ]  |                          |
|Alias Layer Metadata       |  [ ]  |  [ ]  |                          |
