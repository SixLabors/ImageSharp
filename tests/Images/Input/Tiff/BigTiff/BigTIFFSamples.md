These images were created by [AWare Systems](http://www.awaresystems.be/).

# Index

[Classic.tif](#classictif)  
[BigTIFF.tif](#bigtifftif)  
[BigTIFFMotorola.tif](#bigtiffmotorolatif)  
[BigTIFFLong.tif](#bigtifflongtif)  
[BigTIFFLong8.tif](#bigtifflong8tif)  
[BigTIFFMotorolaLongStrips.tif](#bigtiffmotorolalongstripstif)  
[BigTIFFLong8Tiles.tif](#bigtifflong8tilestif)  
[BigTIFFSubIFD4.tif](#bigtiffsubifd4tif)  
[BigTIFFSubIFD8.tif](#bigtiffsubifd8tif)  

# Classic.tif

Classic.tif is a basic Classic TIFF file. All files in this package have the same actual image content, so this TIFF file serves as a reference.

Format: Classic TIFF  
Byte Order: Intel  
Ifd Offset: 12302  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 8  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  

# BigTIFF.tif

BigTIFF.tif ressembles Classic.tif as close as possible. Except that it's a BigTIFF, that is...

Format: BigTIFF  
Byte Order: Intel  
Ifd Offset: 12304  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 16  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  

# BigTIFFMotorola.tif

BigTIFFMotorola.tif reverses the byte order.

Format: BigTIFF  
Byte Order: Motorola  
Ifd Offset: 12304  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 16  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  

# BigTIFFLong.tif

All previous TIFFs specify DataType Short for StripOffsets and StripByteCounts tags. This BigTIFF instead specifies DataType Long, for these tags.

Format: BigTIFF  
Byte Order: Intel  
Ifd Offset: 12304  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Long): 16  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Long): 12288  

# BigTIFFLong8.tif

This next one specifies DataType Long8, for StripOffsets and StripByteCounts tags.

Format: BigTIFF  
Byte Order: Intel  
Ifd Offset: 12304  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Long8): 16  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Long8): 12288  

# BigTIFFMotorolaLongStrips.tif

This BigTIFF has Motorola byte order, plus, it's divided over two strips. StripOffsets and StripByteCounts tags have DataType Long, so their actual value fits inside the IFD.

Format: BigTIFF  
Byte Order: Motorola  
Ifd Offset: 12304  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (2 Long): 16, 6160  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 32  
    StripByteCounts (2 Long): 6144, 6144  

# BigTIFFLong8Tiles.tif

BigTIFFLong8Tiles.tif is a tiled BigTIFF. TileOffsets and TileByteCounts tags specify DataType Long8.

Format: BigTIFF  
Byte Order: Intel  
Ifd Offset: 12368  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    SamplesPerPixel (1 Short): 3  
    TileWidth (1 Short): 32  
    TileLength (1 Short): 32  
    TileOffsets (4 Long8): 16, 3088, 6160, 9232  
    TileByteCounts (4 Long8): 3072, 3072, 3072, 3072  

# BigTIFFSubIFD4.tif

This BigTIFF contains two pages, the second page showing almost the same image content as the first, except that the black square is white, and text color is black. Both pages point to a downsample SubIFD, using SubIFDs DataType TIFF_IFD.

Format: BigTIFF  
Byte Order: Intel  
Ifd Offset: 15572  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 3284  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  
    SubIFDs (1 IFD): 3088  
SubIfd Offset: 3088  
    NewSubFileType (1 Long): 1  
    ImageWidth (1 Short): 32  
    ImageLength (1 Short): 32  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 16  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 32  
    StripByteCounts (1 Short): 3072  
Ifd Offset: 31324  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 19036  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  
    SubIFDs (1 IFD): 18840  
SubIfd Offset: 18840  
    NewSubFileType (1 Long): 1  
    ImageWidth (1 Short): 32  
    ImageLength (1 Short): 32  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 15768  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 32  
    StripByteCounts (1 Short): 3072  

# BigTIFFSubIFD8.tif

BigTIFFSubIFD4.tif is very much the same as BigTIFFSubIFD4.tif, except that the new DataType TIFF_IFD8 is used for the SubIFDs tag.

Format: BigTIFF  
Byte Order: Intel  
Ifd Offset: 15572  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 3284  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  
    SubIFDs (1 IFD8): 3088  
SubIfd Offset: 3088  
    NewSubFileType (1 Long): 1  
    ImageWidth (1 Short): 32  
    ImageLength (1 Short): 32  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 16  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 32  
    StripByteCounts (1 Short): 3072  
Ifd Offset: 31324  
    ImageWidth (1 Short): 64  
    ImageLength (1 Short): 64  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 19036  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 64  
    StripByteCounts (1 Short): 12288  
    SubIFDs (1 IFD8): 18840  
SubIfd Offset: 18840  
    NewSubFileType (1 Long): 1  
    ImageWidth (1 Short): 32  
    ImageLength (1 Short): 32  
    BitsPerSample (3 Short): 8, 8, 8  
    PhotometricInterpretation (1 Short): RGB  
    StripOffsets (1 Short): 15768  
    SamplesPerPixel (1 Short): 3  
    RowsPerStrip (1 Short): 32  
    StripByteCounts (1 Short): 3072