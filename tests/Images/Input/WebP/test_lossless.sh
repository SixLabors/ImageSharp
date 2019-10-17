#!/bin/sh
##
## test_lossless.sh
##
## Simple test to validate decoding of lossless test vectors using
## the dwebp example utility.
##
## This file distributed under the same terms as libwebp. See the libwebp
## COPYING file for more information.
##
set -e

self=$0
usage() {
    cat <<EOT
Usage: $self [options]

Options:
  --exec=/path/to/dwebp
  --extra_args=<dwebp args>
  --formats=format_list (default: $formats)
EOT
    exit 1
}

# Decode $1 as a pam and compare to $2. Additional parameters are passed to the
# executable.
check() {
    local infile="$1"
    local reffile="$2"
    local outfile="$infile.${reffile##*.}"
    shift 2
    printf "${outfile##*/}: "
    eval ${executable} "$infile" $extra_args -o "$outfile" "$@" ${devnull}
    cmp "$outfile" "$reffile"
    echo "OK"

    rm -f "$outfile"
}

# PPM (RGB), PAM (RGBA), PGM (YUV), BMP (BGRA/BGR), TIFF (rgbA/RGB)
formats="ppm pam pgm bmp tiff"
devnull="> /dev/null 2>&1"
for opt; do
    optval=${opt#*=}
    case ${opt} in
        --exec=*) executable="${optval}";;
        --extra_args=*) extra_args="${optval}";;
        --formats=*) formats="${optval}";;
        -v) devnull="";;
        *) usage;;
    esac
done
test_file_dir=$(dirname $self)

executable=${executable:-dwebp}
${executable} 2>/dev/null | grep -q Usage || usage

vectors="0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15"
for i in $vectors; do
    for fmt in $formats; do
        file="$test_file_dir/lossless_vec_1_$i.webp"
        check "$file" "$test_file_dir/grid.$fmt" -$fmt
        check "$file" "$test_file_dir/grid.$fmt" -$fmt -noasm
    done
done

for i in $vectors; do
    for fmt in $formats; do
        file="$test_file_dir/lossless_vec_2_$i.webp"
        check "$file" "$test_file_dir/peak.$fmt" -$fmt
        check "$file" "$test_file_dir/peak.$fmt" -$fmt -noasm
    done
done

for fmt in $formats; do
    file="$test_file_dir/lossless_color_transform.webp"
    check "$file" "$test_file_dir/lossless_color_transform.$fmt" -$fmt
    check "$file" "$test_file_dir/lossless_color_transform.$fmt" -$fmt -noasm
done

echo "ALL TESTS OK"
