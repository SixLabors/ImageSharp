# Open Bitstream Unit

An OBU is a unit of syntax in an AV1 bitstream. HEIF image items can contain still-picture,
progressive, or dependent-frame AV1 payloads, so the decoder handles both intra and inter frames.

An OBU section for AVIF contains the OBUs that follow.

## Temporal delimiter

In AV1 sequences this OBU marks a temporal-unit boundary. Many encoders write one at the start of the payload.

## Sequence header

This is the common header for a sequence of frames. A still picture can use the reduced syntax that
`IsReducedStillPictureHeader` selects. Progressive and dependent-frame payloads use the complete sequence syntax.

## Frame header

Frame-header, redundant-frame-header, and combined-frame OBUs define the syntax of a coded frame.

## Tile group

A tile group OBU gives a range of tiles and holds the entropy-coded data of each tile in that range.

# Tiling

In AV1 a frame contains 1 or more tiles. The frame header gives the tile layout. Each tile is entropy coded
with context-adaptive symbol coding, independently of the other tiles.

## Superblock

A tile contains one or more superblocks. Superblocks are 64x64 or 128x128 pixels.
The sequence header selects the size for all frames of the sequence, and `ObuSequenceHeader` holds it.
A partition tree divides the area of each superblock.

## Partition

The partition type of a node gives how the node divides its area.
A partition can contain more partitions or blocks.

## Block

A block is a leaf of the partition tree. It holds the prediction mode of its area and one or more transform blocks.

## Transform Block

A transform block is an area of a block that uses one transform size and type.

## ModeInfo

Mode information is the smallest unit in the frame. It holds the parameters of an area of 4 by 4 pixels.

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
