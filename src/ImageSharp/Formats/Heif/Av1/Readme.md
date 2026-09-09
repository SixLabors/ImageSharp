# Open Bitstream Unit

An OBU is a unit of syntax encoded in an AV1 bitstream. HEIF image items can contain still-picture,
progressive, or dependent-frame AV1 payloads, so the decoder handles both intra and inter frames.

An OBU section for AVIF consists of the following headers:

## Temporal delimiter

In AV1 sequences this marks a temporal-unit boundary. Many encoders write one at the start of the payload.

## Sequence header

This is the common header for a sequence of frames. A still picture can use the reduced syntax selected by
`ReducedStillPictureHeader`; progressive and dependent-frame payloads use the complete sequence syntax.

## Frame header

Frame-header, redundant-frame-header, and combined-frame OBUs define the syntax of a coded frame.

## Tile group

Defines the tile range and contains the entropy-coded payload for each tile in that range.

# Tiling

In AV1 a frame is made up of 1 or more tiles. The parameters for each tile are entropy encoded using the context aware symbol coding.
These parameters are contained in an OBU tile group header.

## Superblock

A tile consists of one or more superblocks. Superblocks can be either 64x64 or 128x128 pixels in size.
This choice is made per frame, and is specified in the `ObuFrameHeader`.
A superblock contains one or more partitions that subdivide the area.

## Partition

A superblock contains one or more partitions. The partition type determines how the area is split.
Partitions can contain other partitions and blocks.

## Block

## Transform Block

A transform block is the smallest image area that shares transform parameters. A block contains one or more mode-information units.

## ModeInfo

The smallest unit in the frame. It determines the parameters for an area of 4 by 4 pixels.

# References

[AV1 embedded in HEIF](https://aomediacodec.github.io/av1-isobmff)

[AV1 specification](https://aomediacodec.github.io/av1-spec/av1-spec.pdf)

[AVIF specification](https://aomediacodec.github.io/av1-avif)

[Official AV1 reference implementation](https://aomedia.googlesource.com/aom/)

[SVT-AV1 encoder](https://gitlab.com/AOMediaCodec/SVT-AV1)

[libavif AVIF container implementation](https://github.com/AOMediaCodec/libavif)

[Paper describing the techniques used in AV1](https://arxiv.org/pdf/2008.06091)

# Test images

[Netflix image repository](http://download.opencontent.netflix.com/?prefix=AV1/)

[AVIF sample images](https://github.com/link-u/avif-sample-images)
