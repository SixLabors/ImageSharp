#!/bin/sh
##
## test_dwebp.sh
##
## Author: John Koleszar <jkoleszar@google.com>
##
## Simple test driver for validating (via md5 sum) the output of the libwebp
## dwebp example utility.
##
## This file distributed under the same terms as libwebp. See the libwebp
## COPYING file for more information.
##

self=$0

usage() {
    cat <<EOT
Usage: $self [options] [/path/to/libwebp_tests.md5]

Options:
  --exec=/path/to/dwebp
  --md5exec=</path/to/md5sum/replacement> (must support '-c')
  --mt
  --extra_args=<dwebp args>
  --formats=format_list (default: $formats)
  --dump-md5s
EOT
    exit 1
}

# Decode $1 and verify against md5s.
check() {
    local f="$1"
    shift
    # Decode the file to the requested formats.
    for fmt in $formats; do
      eval ${executable} ${mt} -${fmt} ${extra_args} "$@" \
        -o "${f}.${fmt}" "$f" ${devnull}
    done

    if [ "$dump_md5s" = "true" ]; then
      for fmt in $formats; do
        (cd $(dirname $f); ${md5exec} "${f##*/}.${fmt}")
      done
    else
      for fmt in $formats; do
        # Check the md5sums
        grep ${f##*/}.${fmt} "$tests" | (cd $(dirname $f); ${md5exec} -c -) \
          || exit 1
      done
    fi

    # Clean up.
    for fmt in $formats; do
      rm -f "${f}.${fmt}"
    done
}

# PPM (RGB), PAM (RGBA), PGM (YUV), BMP (BGRA/BGR), TIFF (rgbA/RGB)
formats="bmp pam pgm ppm tiff"
mt=""
md5exec="md5sum"
devnull="> /dev/null 2>&1"
dump_md5s="false"
for opt; do
    optval=${opt#*=}
    case ${opt} in
        --exec=*) executable="${optval}";;
        --md5exec=*) md5exec="${optval}";;
        --formats=*) formats="${optval}";;
        --dump-md5s) dump_md5s="true";;
        --mt) mt="-mt";;
        --extra_args=*) extra_args="${optval}";;
        -v) devnull="";;
        -*) usage;;
        *) [ -z "$tests" ] || usage; tests="$opt";;
    esac
done

# Validate test file
if [ -z "$tests" ]; then
    [ -f "$(dirname $self)/libwebp_tests.md5" ] && tests="$(dirname $self)/libwebp_tests.md5"
fi
[ -f "$tests" ] || usage

# Validate test executable
executable=${executable:-dwebp}
${executable} 2>/dev/null | grep -q Usage || usage

test_dir=$(dirname ${tests})
for f in $(grep -o '[[:alnum:]_-]*\.webp' "$tests" | uniq); do
    f="${test_dir}/${f}"
    check "$f"

    if [ "$dump_md5s" = "false" ]; then
      # Decode again, without optimization this time
      check "$f" -noasm
    fi
done

echo "DONE"
