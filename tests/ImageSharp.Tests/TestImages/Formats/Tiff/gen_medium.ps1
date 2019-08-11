$Gm_Exe = "C:\Program Files\ImageMagick-7.0.8-Q16\magick.exe"
$Source_Image = ".\jpeg444_medium.jpg"
$Output_Prefix = ".\jpeg444_medium"

& $Gm_Exe convert $Source_Image -compress None -type TrueColor $Output_Prefix"_rgb_uncompressed.tiff"
& $Gm_Exe convert $Source_Image -compress LZW -type TrueColor $Output_Prefix"_rgb_lzw.tiff"
& $Gm_Exe convert $Source_Image -compress RLE -type TrueColor $Output_Prefix"_rgb_packbits.tiff"
& $Gm_Exe convert $Source_Image -compress JPEG -type TrueColor $Output_Prefix"_rgb_jpeg.tiff"
& $Gm_Exe convert $Source_Image -compress Zip -type TrueColor $Output_Prefix"_rgb_deflate.tiff"

& $Gm_Exe convert $Source_Image -compress None -type Grayscale $Output_Prefix"_grayscale_uncompressed.tiff"
& $Gm_Exe convert $Source_Image -compress None -colors 256 $Output_Prefix"_palette_uncompressed.tiff"