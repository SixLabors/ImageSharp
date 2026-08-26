# AV1 deblocking conformance fixtures

The AVIF files come from `libavif/tests/data` at commit `062e582e8afda88e6baf988fdcf046a801efa0f5`. They retain the licenses recorded in libavif's `tests/data/README.md`: the Kodak image is released for unrestricted use, the Cosmos Laundromat frame uses CC BY 3.0, and the libavif color animation is distributed with the libavif test corpus under its BSD-2-Clause license.

The 8- and 10-bit `.bit` files contain the exact AV1 item payloads from the corresponding AVIF files. Each still file has one item occupying the complete `mdat` payload. The genuine 12-bit libavif sequence is retained for container, presentation, alpha, and metadata coverage, but its first color frame disables deblocking and therefore cannot prove the 12-bit filter path.

`libaom-cosmos1650-12b.bit` was encoded from libavif's real 10-bit 4:4:4 Cosmos Laundromat Y4M source with the pinned libaom encoder. libaom promotes the input samples to a 12-bit AV1 profile-2 still-picture stream. The constant-quality level is deliberately lossy so the frame signals nonzero loop-filter levels. The material command options were `--usage=2 --passes=1 --limit=1 --obu --bit-depth=12 --input-bit-depth=10 --profile=2 --end-usage=q --cq-level=30 --cpu-used=6 --threads=1 --lag-in-frames=0 --full-still-picture-hdr`.

The `_libaom.yuv` files were decoded from those exact payloads with `aomdec` built from libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f`. The reference build used `AOM_TARGET_CPU=generic`, so these files come from libaom's scalar decoder rather than ImageSharp or an architecture-specific implementation.

The native reference layouts are:

- `libavif-kodim23-8b-libaom.yuv`: 768x512, 8-bit YUV 4:2:0, planar Y/U/V.
- `libavif-cosmos1650-10b-libaom.yuv`: 1024x428, 10-bit YUV 4:4:4, planar Y/U/V with little-endian 16-bit samples.
- `libaom-cosmos1650-12b-libaom.yuv`: 1024x428, 12-bit YUV 4:4:4, planar Y/U/V with little-endian 16-bit samples.

The conformance test compares every visible reconstructed sample with these files. It also verifies that the parsed frame enables deblocking, so a disabled or bypassed loop-filter stage cannot satisfy the test accidentally.
