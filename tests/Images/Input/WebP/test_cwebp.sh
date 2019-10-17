#!/bin/sh
##
## test_cwebp.sh
##
## Simple test to validate encoding of source images using the cwebp
## example utility.
##
## This file distributed under the same terms as libwebp. See the libwebp
## COPYING file for more information.
##

self=$0

usage() {
    cat <<EOT
Usage: $self [options] <source files to test>

Options:
  --exec=</path/to/cwebp>
  --md5exec=</path/to/md5sum/replacement>
  --loop=<count>
  --nocheck
  --mt
  --noalpha
  --lossless
  --extra_args=<cwebp args>
EOT
    exit 1
}

run() {
    # simple means for a batch speed test
    ${executable} $file
}

check() {
    # test the optimized vs. unoptimized versions. this is a bit
    # fragile, but good enough for optimization testing.
    md5=$({ ${executable} -o - $file || echo "fail1"; } | ${md5exec})
    md5_noasm=$( { ${executable} -noasm -o - $file || echo "fail2"; } | ${md5exec})

    printf "$file:\t"
    if [ "$md5" = "$md5_noasm" ]; then
        printf "OK\n"
    else
        printf "FAILED\n"
        exit 1
    fi
}

check="true"
noalpha=""
lossless=""
mt=""
md5exec="md5sum"
extra_args=""

n=1
for opt; do
    optval=${opt#*=}
    case ${opt} in
        --exec=*) executable="${optval}";;
        --md5exec=*) md5exec="${optval}";;
        --loop=*) n="${optval}";;
        --mt) mt="-mt";;
        --lossless) lossless="-lossless";;
        --noalpha) noalpha="-noalpha";;
        --nocheck) check="";;
        --extra_args=*) extra_args="${optval}";;
        -*) usage;;
        *) break;;
    esac
    shift
done

[ $# -gt 0 ] || usage
[ "$n" -gt 0 ] || usage

executable=${executable:-cwebp}
${executable} 2>/dev/null | grep -q Usage || usage
executable="${executable} -quiet ${mt} ${lossless} ${noalpha} ${extra_args}"
set +e

if [ "$check" = "true" ]; then
    TEST=check
else
    TEST=run
fi

N=$n
while [ $n -gt 0 ]; do
    for file; do
        $TEST
    done
    n=$((n - 1))
    printf "DONE (%d of %d)\n" $(($N - $n)) $N
done
