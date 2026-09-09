// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#include <stdint.h>
#include <stdlib.h>
#include "aom/aom_encoder.h"
#include "aom/aomcx.h"

#if defined(_WIN32)
#define BENCHMARK_API __declspec(dllexport)
#else
#define BENCHMARK_API __attribute__((visibility("default")))
#endif

// Keep libaom's version-dependent structures and variadic controls entirely on the C side.
// The managed benchmark exchanges only opaque ownership, fixed-width integers, and borrowed buffers.
typedef struct benchmark_encoder {
  aom_codec_ctx_t codec;
  aom_codec_iter_t iterator;
  unsigned int width;
  unsigned int height;
} benchmark_encoder;

// Creates one single-threaded, unlagged, constant-quality sequence using the reference's normal tools.
// A successful result belongs to the caller and must be released by benchmark_destroy.
BENCHMARK_API int benchmark_create(unsigned int width, unsigned int height,
                                  unsigned int quality, int speed,
                                  benchmark_encoder **result) {
  aom_codec_enc_cfg_t config;
  aom_codec_iface_t *iface = aom_codec_av1_cx();
  aom_codec_err_t status = aom_codec_enc_config_default(iface, &config, AOM_USAGE_GOOD_QUALITY);
  *result = NULL;
  if (status != AOM_CODEC_OK) return status;

  benchmark_encoder *encoder = calloc(1, sizeof(*encoder));
  if (encoder == NULL) return AOM_CODEC_MEM_ERROR;

  config.g_w = width;
  config.g_h = height;
  config.g_threads = 1;
  config.g_lag_in_frames = 0;
  config.g_timebase.num = 1;
  config.g_timebase.den = 30;
  config.rc_end_usage = AOM_Q;
  // Fix both bounds to the requested quantizer. CQ alone permits frame-quality boosts, whereas
  // the managed sequence uses this same base quantizer for every frame in the comparison.
  config.rc_min_quantizer = quality;
  config.rc_max_quantizer = quality;
  config.kf_mode = AOM_KF_DISABLED;
  status = aom_codec_enc_init(&encoder->codec, iface, &config, 0);
  if (status != AOM_CODEC_OK) {
    free(encoder);
    return status;
  }

  // These calls remain type-checked against the current libaom headers, including each control's argument type.
  status = aom_codec_control(&encoder->codec, AOME_SET_CPUUSED, speed);
  if (status == AOM_CODEC_OK) status = aom_codec_control(&encoder->codec, AOME_SET_CQ_LEVEL, quality);
  if (status == AOM_CODEC_OK) status = aom_codec_control(&encoder->codec, AV1E_SET_ROW_MT, 0u);
  if (status == AOM_CODEC_OK) status = aom_codec_control(&encoder->codec, AV1E_SET_COLOR_PRIMARIES, AOM_CICP_CP_BT_601);
  if (status == AOM_CODEC_OK) status = aom_codec_control(&encoder->codec, AV1E_SET_TRANSFER_CHARACTERISTICS, AOM_CICP_TC_BT_601);
  if (status == AOM_CODEC_OK) status = aom_codec_control(&encoder->codec, AV1E_SET_MATRIX_COEFFICIENTS, AOM_CICP_MC_BT_601);
  if (status == AOM_CODEC_OK) status = aom_codec_control(&encoder->codec, AV1E_SET_COLOR_RANGE, AOM_CR_FULL_RANGE);
  if (status != AOM_CODEC_OK) {
    aom_codec_destroy(&encoder->codec);
    free(encoder);
    return status;
  }

  encoder->width = width;
  encoder->height = height;
  *result = encoder;
  return AOM_CODEC_OK;
}

// Borrows the already converted planes for this synchronous encode call. The caller pins all three
// pointers until it returns; libaom retains its own reference pictures, never these managed input pointers.
BENCHMARK_API int benchmark_encode(benchmark_encoder *encoder, unsigned char *y,
                                  unsigned char *u, unsigned char *v,
                                  int y_stride, int uv_stride, int64_t frame_index) {
  aom_image_t image;
  if (aom_img_wrap(&image, AOM_IMG_FMT_I420, encoder->width, encoder->height, 1, y) == NULL) {
    return AOM_CODEC_INVALID_PARAM;
  }

  // ImageSharp's source planes include aligned borders. Describe those existing rows directly instead
  // of flattening them into another contiguous YUV allocation before calling the reference encoder.
  image.planes[AOM_PLANE_Y] = y;
  image.planes[AOM_PLANE_U] = u;
  image.planes[AOM_PLANE_V] = v;
  image.stride[AOM_PLANE_Y] = y_stride;
  image.stride[AOM_PLANE_U] = uv_stride;
  image.stride[AOM_PLANE_V] = uv_stride;
  encoder->iterator = NULL;
  return aom_codec_encode(&encoder->codec, &image, frame_index, 1, 0);
}

// Drains delayed output at the end of the sequence even though lookahead is disabled.
BENCHMARK_API int benchmark_flush(benchmark_encoder *encoder) {
  encoder->iterator = NULL;
  return aom_codec_encode(&encoder->codec, NULL, -1, 1, 0);
}

// The returned packet storage belongs to libaom and is valid only until the next codec call.
// The managed side copies it directly into the same kind of destination stream as its own encoder.
BENCHMARK_API int benchmark_next_packet(benchmark_encoder *encoder, const void **data, size_t *length) {
  const aom_codec_cx_pkt_t *packet;
  while ((packet = aom_codec_get_cx_data(&encoder->codec, &encoder->iterator)) != NULL) {
    if (packet->kind == AOM_CODEC_CX_FRAME_PKT) {
      *data = packet->data.frame.buf;
      *length = packet->data.frame.sz;
      return 1;
    }
  }

  *data = NULL;
  *length = 0;
  return 0;
}

// Releases exactly the native context allocated by benchmark_create.
BENCHMARK_API int benchmark_destroy(benchmark_encoder *encoder) {
  aom_codec_err_t status = aom_codec_destroy(&encoder->codec);
  free(encoder);
  return status;
}

// Libaom owns this static UTF-8 error string; the caller must not free it.
BENCHMARK_API const char *benchmark_error_string(int status) {
  return aom_codec_err_to_string((aom_codec_err_t)status);
}
