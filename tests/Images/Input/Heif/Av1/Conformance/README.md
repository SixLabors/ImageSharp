# AV1 reconstruction conformance fixtures

The original AVIF and Y4M source files come from `libavif/tests/data` at commit `062e582e8afda88e6baf988fdcf046a801efa0f5`. Derived fixtures retain the licenses recorded in libavif's `tests/data/README.md`: the Kodak image is released for unrestricted use, the Cosmos Laundromat frame uses CC BY 3.0, and the libavif color animation is distributed with the libavif test corpus under its BSD-2-Clause license.

The 8- and 10-bit `.bit` files contain the exact AV1 item payloads from the corresponding AVIF files. Each still file has one item occupying the complete `mdat` payload. The genuine 12-bit libavif sequence is retained for container, presentation, alpha, and metadata coverage, but its first color frame disables deblocking and therefore cannot prove the 12-bit filter path.

`libaom-cosmos1650-12b.bit` was encoded from libavif's real 10-bit 4:4:4 Cosmos Laundromat Y4M source with the pinned libaom encoder. libaom promotes the input samples to a 12-bit AV1 profile-2 still-picture stream. The constant-quality level is deliberately lossy so the frame signals nonzero loop-filter levels. The material command options were `--usage=2 --passes=1 --limit=1 --obu --bit-depth=12 --input-bit-depth=10 --profile=2 --end-usage=q --cq-level=30 --cpu-used=6 --threads=1 --lag-in-frames=0 --full-still-picture-hdr`.

The `_libaom.yuv` files were decoded from those exact payloads with `aomdec` built from libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f`. The reference build used `AOM_TARGET_CPU=generic`, so these files come from libaom's scalar decoder rather than ImageSharp or an architecture-specific implementation.

The `libaom-cdef-*` elementary streams were encoded separately with the same pinned generic libaom build so CDEF could be verified independently of the original corpus. The 8-bit stream uses `kodim23_yuv420_8bpc.y4m`; the 10- and 12-bit streams use `cosmos1650_yuv444_10bpc_p3pq.y4m`. Both source files are retained in libavif's test data at commit `062e582e8afda88e6baf988fdcf046a801efa0f5`.

The material encoder options were `--usage=2 --passes=1 --limit=1 --obu --end-usage=q --cq-level=30 --cpu-used=4 --threads=1 --lag-in-frames=0 --full-still-picture-hdr --enable-cdef=1 --enable-restoration=0`. Each command also supplied the matching `--bit-depth`, `--input-bit-depth`, and `--profile` values. The 12-bit stream promotes the 10-bit 4:4:4 input through libaom's native 12-bit pipeline. Loop restoration is explicitly disabled so exact output equality exercises deblocking followed by active CDEF without a later restoration stage changing those samples.

The `libavif-cdef-*` AVIF files were independently encoded with `avifenc` 1.4.2 from libavif commit `062e582e8afda88e6baf988fdcf046a801efa0f5` and its pinned libaom 3.14.1 dependency. The material options were `-j 1 -s 4 -q 60`, `enable-cdef=1`, and `enable-restoration=0`. The 8-bit 4:2:0 file uses CICP 1/13/6 and the Kodak Y4M source. The 10-bit 4:4:4 file uses CICP 12/16/12 and the Cosmos Laundromat Y4M source. The 12-bit 4:4:4 input wraps the pinned 12-bit scalar-libaom reference planes as `C444p12` Y4M and also uses CICP 12/16/12.

The matching `.png` files were produced by `avifdec` from the same scalar build with `-j 1 -d 8`; the 8-bit 4:2:0 reference additionally selected bilinear chroma upsampling. The build uses `AOM_TARGET_CPU=generic` and `AVIF_LIBYUV=OFF`, so both AV1 reconstruction and YUV-to-RGB presentation come from the pinned scalar libaom/libavif paths. ImageSharp compares every presented RGBA byte exactly, without a tolerance.

The native reference layouts are:

- `libavif-kodim23-8b-libaom.yuv`: 768x512, 8-bit YUV 4:2:0, planar Y/U/V.
- `libavif-cosmos1650-10b-libaom.yuv`: 1024x428, 10-bit YUV 4:4:4, planar Y/U/V with little-endian 16-bit samples.
- `libaom-cosmos1650-12b-libaom.yuv`: 1024x428, 12-bit YUV 4:4:4, planar Y/U/V with little-endian 16-bit samples.
- `libaom-cdef-kodim23-8b-libaom.yuv`: 768x512, 8-bit YUV 4:2:0, planar Y/U/V.
- `libaom-cdef-cosmos-10b-libaom.yuv`: 1024x428, 10-bit YUV 4:4:4, planar Y/U/V with little-endian 16-bit samples.
- `libaom-cdef-cosmos-12b-libaom.yuv`: 1024x428, 12-bit YUV 4:4:4, planar Y/U/V with little-endian 16-bit samples.

The conformance tests compare every visible reconstructed sample with these files. The deblocking corpus verifies nonzero loop-filter levels. The CDEF corpus additionally verifies sequence-level CDEF enablement, a selected nonzero frame strength, and disabled loop restoration, so a disabled or bypassed CDEF stage cannot satisfy the exact native-plane comparison accidentally.

The `libaom-superres-*` streams were encoded from the same Kodak and Cosmos sources with the pinned generic libaom build. Their material options were `--usage=2 --passes=1 --limit=1 --obu --end-usage=q --cq-level=30 --cpu-used=4 --threads=1 --lag-in-frames=0 --full-still-picture-hdr --enable-cdef=0 --enable-restoration=0 --superres-mode=1 --superres-denominator=12 --superres-kf-denominator=12`, together with the matching input depth, output depth, and profile. Disabling CDEF and restoration isolates the normative horizontal upscaling result, while the tests separately require a coded width smaller than the displayed width so an unscaled stream cannot satisfy the reference comparison.

The matching `libaom-superres-*-libaom.yuv` files were decoded by `aomdec --rawvideo` from that exact generic build. They retain the displayed 768x512 8-bit YUV 4:2:0 and 1024x428 10/12-bit YUV 4:4:4 layouts described above.
