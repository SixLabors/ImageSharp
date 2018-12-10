$Gm_Exe = "C:\Program Files\GraphicsMagick-1.3.25-Q16\gm.exe"
$Source_Image = "..\Jpg\baseline\Calliphora.jpg"
$Output_Prefix = ".\Calliphora"

& $Gm_Exe convert $Source_Image -compress None -type TrueColor $Output_Prefix"_rgb_uncompressed.tiff"
& $Gm_Exe convert $Source_Image -compress LZW -type TrueColor $Output_Prefix"_rgb_lzw.tiff"
& $Gm_Exe convert $Source_Image -compress RLE -type TrueColor $Output_Prefix"_rgb_packbits.tiff"
& $Gm_Exe convert $Source_Image -compress JPEG -type TrueColor $Output_Prefix"_rgb_jpeg.tiff"
& $Gm_Exe convert $Source_Image -compress Zip -type TrueColor $Output_Prefix"_rgb_deflate.tiff"

& $Gm_Exe convert $Source_Image -compress None -type Grayscale $Output_Prefix"_grayscale_uncompressed.tiff"
& $Gm_Exe convert $Source_Image -compress None -colors 256 $Output_Prefix"_palette_uncompressed.tiff"