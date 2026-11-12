//! One-shot, single-threaded JPEG XL decoding for in-process codec benchmarks.
//!
//! Each call creates and drops an original jxl-rs decoder. The decoder writes RGBA f32 directly
//! into the returned allocation. There is no persistent decoder, output copy or worker pool.
//! The caller must release each successful result before reusing its result structure.

use std::mem::ManuallyDrop;
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::ptr;
use std::slice;

use jxl::api::{
    AlphaMode, Event, JxlColorEncoding, JxlColorProfile, JxlDecoder, JxlDecoderOptions,
    JxlOutputBuffer, JxlPixelFormat,
};

/// A Rust-owned, contiguous RGBA f32 image in row-major pixel order.
///
/// `length` and `capacity` count float samples, not bytes or pixels. Each pixel has four samples.
/// `capacity` belongs to the Rust allocation and must remain unchanged until release.
/// RGB uses nonlinear sRGB and alpha is unassociated. Samples are not bounded by this bridge;
/// the benchmark caller applies its destination pixel contract when comparing results.
#[repr(C)]
pub struct DecodeResult {
    /// First RGBA sample, or null when decoding failed or the result has been released.
    pub pixels: *mut f32,
    /// Number of initialized float samples: width times height times four.
    pub length: usize,
    /// Original Vec capacity required to release the allocation without copying it.
    pub capacity: usize,
    /// Output width in pixels, before any orientation adjustment.
    pub width: usize,
    /// Output height in pixels, before any orientation adjustment.
    pub height: usize,
}

/// A completed image whose allocation remains owned by Rust until FFI transfer succeeds.
struct OwnedImage {
    /// Direct decoder output, including alpha supplied by the original saver.
    pixels: Vec<f32>,
    /// Width in pixels.
    width: usize,
    /// Height in pixels.
    height: usize,
}

/// Decodes one still image with a new decoder and no parallel runner.
///
/// The original public API has no output-profile setter. Verify its actual output before decoding
/// sections, rather than assuming that requesting float output also requests nonlinear sRGB.
/// This benchmark bridge rejects other profiles instead of adding an unmatched CMS pass.
fn decode(input: &[u8]) -> Result<OwnedImage, i32> {
    let mut options = JxlDecoderOptions::default();
    options.high_precision = true;
    options.adjust_orientation = false;
    options.alpha_mode = AlphaMode::Unpremultiplied;
    let mut decoder = JxlDecoder::new(options);
    let mut input = input;
    let mut output: Option<OwnedImage> = None;

    loop {
        let event = if let Some(image) = output.as_mut() {
            let bytes_per_row = image.width * 4 * size_of::<f32>();
            // SAFETY: Vec<f32> supplies aligned, initialized, exclusive storage for every output
            // byte. This temporary byte borrow ends with process(), before the Vec is accessed again.
            let bytes = unsafe {
                slice::from_raw_parts_mut(
                    image.pixels.as_mut_ptr().cast::<u8>(),
                    image.pixels.len() * size_of::<f32>(),
                )
            };
            let mut buffers = [JxlOutputBuffer::new(bytes, image.height, bytes_per_row)];
            // None selects the crate's normal SequentialRunner. It does not create a Rayon pool.
            decoder.process(&mut input, Some(&mut buffers), None)
        } else {
            decoder.process(&mut input, None, None)
        }
        .map_err(|_| 2)?;

        match event {
            Event::BasicInfo => {
                let info = decoder.basic_info().ok_or(2)?;
                if info.animation.is_some() {
                    return Err(5);
                }

                let (width, height) = info.size;
                let extra_channels = info.extra_channels.len();
                let length = width
                    .checked_mul(height)
                    .and_then(|pixels| pixels.checked_mul(4))
                    .filter(|&samples| samples <= isize::MAX as usize / size_of::<f32>())
                    .ok_or(2)?;
                decoder
                    .set_pixel_format(JxlPixelFormat::rgba_f32(extra_channels))
                    .map_err(|_| 2)?;
                let srgb = JxlColorProfile::Simple(JxlColorEncoding::srgb(false));
                if !decoder
                    .output_color_profile()
                    .is_some_and(|profile| profile.same_color_encoding(&srgb))
                {
                    return Err(3);
                }

                // The original output-buffer API requires initialized storage. The decoder writes
                // its final pixels here, including alpha=1 when no alpha channel exists.
                output = Some(OwnedImage {
                    pixels: vec![0.0; length],
                    width,
                    height,
                });
            }
            Event::FrameHeader => {}
            Event::FrameComplete { has_more_frames } => {
                if has_more_frames {
                    return Err(5);
                }
            }
            Event::Complete => return output.ok_or(2),
            Event::NeedMoreInput { .. } => return Err(2),
        }
    }
}

/// Decodes one complete JPEG XL still image directly into Rust-owned RGBA f32 storage.
///
/// Status: 0 success; 1 invalid FFI arguments; 2 invalid/truncated input or decoder error;
/// 3 output is not nonlinear sRGB; 4 caught Rust panic; 5 animation is outside this benchmark.
/// Every failure clears the result. A successful result must be passed to jxlrs_release_rgba_f32.
///
/// # Safety
/// `data` must address `length` readable bytes for this call. `result` must address one writable,
/// properly aligned DecodeResult and must not contain an unreleased successful result.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_decode_rgba_f32(
    data: *const u8,
    length: usize,
    result: *mut DecodeResult,
) -> i32 {
    if result.is_null() {
        return 1;
    }

    // SAFETY: The caller supplies one writable result. Clearing it gives every failure one state.
    unsafe { ptr::write_bytes(result, 0, 1) };
    if data.is_null() || length == 0 || length > isize::MAX as usize {
        return 1;
    }

    let decoded = catch_unwind(AssertUnwindSafe(|| {
        // SAFETY: The caller promises the input range is readable for the whole decode.
        decode(unsafe { slice::from_raw_parts(data, length) })
    }));
    match decoded {
        Ok(Ok(image)) => {
            let mut pixels = ManuallyDrop::new(image.pixels);
            // Transfer the original Vec allocation without shrinking, copying or allocating a handle.
            // SAFETY: The caller's result is writable, and Rust retains no access to these samples.
            unsafe {
                result.write(DecodeResult {
                    pixels: pixels.as_mut_ptr(),
                    length: pixels.len(),
                    capacity: pixels.capacity(),
                    width: image.width,
                    height: image.height,
                });
            }
            0
        }
        Ok(Err(status)) => status,
        Err(_) => 4,
    }
}

/// Releases an owned result and clears its fields. A cleared result can be released again.
///
/// # Safety
/// `result` must be null, a writable cleared result, or an unchanged successful result from
/// jxlrs_decode_rgba_f32. No sample pointer may be used during or after release.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_release_rgba_f32(result: *mut DecodeResult) {
    if result.is_null() {
        return;
    }

    // SAFETY: The caller supplies a valid result and sole ownership of any returned allocation.
    unsafe {
        let image = result.read();
        ptr::write_bytes(result, 0, 1);
        if !image.pixels.is_null() {
            drop(Vec::from_raw_parts(
                image.pixels,
                image.length,
                image.capacity,
            ));
        }
    }
}
