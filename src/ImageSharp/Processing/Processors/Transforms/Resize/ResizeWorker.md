# ResizeWorker

This document describes how `ResizeWorker<TPixel>` resizes an image, and how its buffers and SIMD operators work.
`HeifPlanarAlphaResizeWorker` uses the same design for a single alpha plane.

Read it with these files:

- `ResizeWorker.cs`: the sliding window, the source row conversion, and the destination row packing.
- `ResizeKernelMap.cs` and `ResizeKernelMap.PeriodicKernelMap.cs`: the kernels and their weights.
- `ResizeKernel.Operators.cs`: the horizontal and vertical SIMD operators.

## 1. Overview

The resize is separable. It has two passes:

1. The **horizontal pass** (first pass) convolves each source row with the horizontal kernels. It makes one first-pass
   row for each source row. A first-pass row has one pixel for each destination X.
2. The **vertical pass** convolves the first-pass rows with the vertical kernels. It makes one destination row for each
   destination Y.

```
 source                     first pass                   destination
 (sourceWidth x             (destinationWidth x          (destinationWidth x
  sourceHeight)              sourceHeight)                destinationHeight)

 +--------------+           +------+                     +------+
 |              |  horiz.   |      |      vertical       |      |
 |              | --------> |      | ------------------> |      |
 |              |  kernels  |      |      kernels        +------+
 |              |           |      |
 +--------------+           +------+
```

The worker does not store the full first pass. It keeps only a window of first-pass rows. The window moves down the
source as the vertical pass needs new rows.

All pixel values in both passes are `Vector4` scaled vectors: 4 floats, RGBA, in the range of the pixel format.

## 2. Kernels and weights

`ResizeKernelMap` holds one `ResizeKernel` for each destination sample. A kernel has:

- `StartIndex`: the first source sample.
- `Length`: the number of source samples.
- `WeightsOffset`: the position of its weights in the weight buffer of the map.

The weight buffer stores each weight 4 times, once for each channel of a pixel. Then a load of N weights has the same
layout as a load of N pixels:

```
 weights  | w0 w0 w0 w0 | w1 w1 w1 w1 | w2 w2 w2 w2 | ... |
 pixels   | r0 g0 b0 a0 | r1 g1 b1 a1 | r2 g2 b2 a2 | ... |
           \___ tap 0 _/ \___ tap 1 _/ \___ tap 2 _/
```

One multiply gives `w[t] * pixel[t]` for each channel of each tap in the register.

### Padded length

`MaxDiameter` is the number of source samples of each kernel. `PaddedLength` is the number of taps that the horizontal
operators convolve. It is a multiple of 4, because the operators read 4 taps in each step. The taps after the length of
a kernel have the weight zero.

```
 kernel row   | t0 | t1 | ... | t34 | 0 | <- PaddedLength = 36 taps (MaxDiameter = 35)
```

A kernel at the end of a source row reads up to `PaddedLength - MaxDiameter` pixels after the row. The workers put
finite values in these pixels, so a weight of zero times these pixels gives zero.

### Aligned starts (horizontal maps only)

A pixel is 16 bytes, so 4 pixels are 64 bytes: one cache line, and one `Vector512<float>`. If a kernel starts at a
pixel that is not a multiple of 4, each 4-pixel load crosses a cache line, and costs 2 cache accesses.

With `alignKernelStarts`, the map moves each kernel start to the left, to a multiple of 4. It puts s = 0 to 3 weights of
zero in front of the real weights:

```
 source pixel   ...| 40 | 41 | 42 | 43 | 44 | 45 | ...
 exact start               ^ 42
 aligned start   ^ 40
 kernel row      |  0 |  0 | w0 | w1 | w2 | ...        s = 2, Length = MaxDiameter + 2
```

The products do not change, and the result has the same bits (see section 6). The vertical maps keep exact starts,
because the vertical operators convolve `MaxDiameter` rows from the start of each kernel.

### Periodic maps

For most sizes, the kernels in the middle repeat: kernel `i + period` has the same weights as kernel `i`, and starts
`sourcePeriod` samples later. `PeriodicKernelMap` calculates the weights of the corner kernels and of one period with
the sampler. The other kernels use the rows of that period.

With aligned starts, a copy needs a different shift if `sourcePeriod` is not a multiple of 4. The shift of period j is
`(s + j * sourcePeriod) % 4`. It repeats after 1, 2 or 4 periods. So each kernel of the period has one row for each
shift variant. The sampler calculates the first row, and the map copies those weights to the other rows, after the new
number of zeros.

```
 weight rows   | top corners | period, variant 0 | period, variant 1 | ... | bottom corners |
                               ^ sampler            ^ copies of variant 0
```

## 3. The sliding window

The window holds `workerHeight` first-pass rows. Window row Y holds the first-pass row of source row
`currentWindow.Min + Y`. The rows are contiguous and row-major: a first-pass row is `destinationWidth` pixels.

- `windowBandHeight` is the `MaxDiameter` of the vertical map. One band holds all rows of the longest vertical kernel.
- `workerHeight` is a whole number of bands, at least 2. `Configuration.WorkingBufferSizeHintInBytes` sets the
  upper limit. If the source has fewer rows, `workerHeight` is the source height.

```
 first-pass buffer (workerHeight x destinationWidth Vector4)

            destination X ->
          +----------------------------------+
 window   | row 0  = source row Min + 0      |  band 0
 row Y    | row 1  = source row Min + 1      |
   |      | ...                              |
   v      +----------------------------------+
          | ...                              |  band 1
          +----------------------------------+
          | ...                              |  ...
          +----------------------------------+
          | row workerHeight - 1             |  last band
          +----------------------------------+
```

`Initialize` calculates the first window, source rows 0 to `workerHeight - 1`.

## 4. The vertical pass and the slide

`FillDestinationPixels` makes the destination rows in increasing Y. For each destination row:

1. Get the vertical kernel of the row.
2. If the kernel ends after the window (`StartIndex + Length > currentWindow.Max`), slide the window.
3. Convolve the kernel on its rows of the window (`ResizeKernel.ConvolveColumns`).
4. Pack the result to the destination pixel format.

A slide keeps the last band, because the next kernels can still use its rows. It copies the last band to the top of the
buffer in one copy. Then the first pass calculates the other rows of the new window:

```
 before the slide                        after the slide
 +-----------------------+               +-----------------------+
 | band 0                |      +------> | old last band (copy)  |  source rows Max-band .. Max-1
 +-----------------------+      |        +-----------------------+
 | band 1                |      |        | new rows (calculated) |
 +-----------------------+      |        +-----------------------+
 | last band             | -----+        | new rows (calculated) |
 +-----------------------+               +-----------------------+
 source rows Min .. Max-1                source rows Max-band .. Max-band+workerHeight-1
```

The new window ends at the last source row if the source has fewer rows. The window holds at least 2 bands, so the
copy source and the copy destination do not overlap.

### Vertical operator lanes

All pixels of one destination row use the same vertical kernel. So one register holds neighboring destination pixels,
and one broadcast weight multiplies all of them for one tap:

```
 Vector512VerticalOperator, 4 neighboring destination pixels x .. x+3

 tap t:  sum += broadcast(w[t]) * | px(x) | px(x+1) | px(x+2) | px(x+3) |   from first-pass row StartIndex + t
                                     lane 0  lane 1    lane 2    lane 3

 result: | dest(x) | dest(x+1) | dest(x+2) | dest(x+3) |
```

The loop adds the taps in increasing t. No reduction is necessary. `Vector256VerticalOperator` does the same for 2
pixels, and `Vector128VerticalOperator` for 1 pixel.

## 5. The horizontal pass

`CalculateFirstPassValues` calculates the first-pass rows of a row interval. It processes 2 source rows in each step:

1. Convert source rows y and y + 1 to scaled vectors (`ConvertSourceRow`).
2. Convolve both rows with all horizontal kernels (`ResizeKernel.ConvolveRows`).
3. Store the results in window rows `y - Min` and `y + 1 - Min`.

If the interval has an odd number of rows, the last step gives the same row to both inputs. The operators calculate the
same values twice, and store them to the same place.

### The source row buffer

The two source rows are in one float buffer. The memory allocator does not align its buffers, so the buffer has 15
more floats than the rows need. The rows start at the first 64-byte boundary in the buffer:

```
 buffer (floats)
 | 0..15 floats | row 0 pixels | padding | round up | row 1 pixels | padding | round up |
 ^ buffer start ^ 64-byte boundary                  ^ 64-byte boundary
                \________ tempRowStride ___________/
```

- The padding is `PaddedLength - MaxDiameter` pixels. It is zero, or old finite pixels of an earlier row.
- `tempRowStride` rounds each row up to a multiple of 4 pixels (64 bytes), so the second row is also aligned.
- The worker gets the offset again for each interval, because the garbage collector can move a managed buffer.
  A different offset changes only the speed, not the results.

With aligned kernel starts, every pixel load of the horizontal operators then starts on a cache line.

### Horizontal operator lanes

Each destination pixel has its own kernel. `Vector512HorizontalOperator` convolves 4 neighboring kernels on both source
rows. One step reads 4 taps (16 floats) of each kernel:

```
 step at taps t .. t+3, kernel K, row R

   weights K   | w[t]   x4 | w[t+1] x4 | w[t+2] x4 | w[t+3] x4 |   one load, used for row 0 and row 1
   pixels R,K  | px[t]     | px[t+1]   | px[t+2]   | px[t+3]   |   one load for each row
   sum R,K    += product      lane 0      lane 1      lane 2      lane 3
```

Lane l of an accumulator holds partial sum `pl`: the RGBA sum of the taps t with `t % 4 == l`. The 4 kernels and 2 rows
give 8 accumulators. Each weight load serves 2 multiply-adds, so a step needs 12 loads for 8 multiply-adds, not 16.

After the loop, `Reduce` adds the partial sums and transposes the 4 kernels into one register, in 2 stages:

```
 input       sum0 = | p00 | p01 | p02 | p03 |     (kernel 0)   ... sum3 (kernel 3)

 stage 1     sum01 = | p00+p02 | p01+p03 | p10+p12 | p11+p13 |
             sum23 = | p20+p22 | p21+p23 | p30+p32 | p31+p33 |

 stage 2     | k0 (p0+p2) | k1 (p0+p2) | k2 (p0+p2) | k3 (p0+p2) |
           + | k0 (p1+p3) | k1 (p1+p3) | k2 (p1+p3) | k3 (p1+p3) |

 result      | dest(x)    | dest(x+1)  | dest(x+2)  | dest(x+3)  |
```

`Vector256HorizontalOperator` convolves 2 kernels, and `Vector128HorizontalOperator` 1 kernel. They keep the same
partial sums, and add them in the same order: `(p0 + p2) + (p1 + p3)`.

`ConvolveRows` uses the widest accelerated operator for most kernels, then the narrower operators for the kernels that
remain. The Vector128 operators also serve the path without hardware acceleration.

## 6. Results

All register widths give the same bits on one machine:

- Horizontal: each width adds the same partial sums in the order `(p0 + p2) + (p1 + p3)`.
- Vertical: each width adds the taps in increasing t.
- Two rows: each row has its own accumulators.

Aligned starts also give the same bits. With s zeros in front, tap t goes to partial sum `(t + s) % 4`. These are the
same 4 sums in a rotated order, and the reduction still adds the pairs `{p0, p2}` and `{p1, p3}`, with the operands
swapped. Float addition is commutative. The only possible difference is the sign of a result that is zero, and both
signs give the same pixel.

`MultiplyAddEstimate` can fuse the multiply and the add, or not, as the hardware permits. Thus results can differ
between machines in the last bit.
