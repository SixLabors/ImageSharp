# Implementation of Modular coding mode for JPEG XL
JPEG XL files typically consist of two distinct coding modes: VarDCT
and Modular. VarDCT is lossy and transform-based, while Modular uses
the predictive coding approach and can achieve lossless or near-lossless
compression.

This folder has the implementation of the Modular encoder/decoder.

### Folder structure
In the Transforms folder, there are implementations for the Reversible
Color Transform (RCT), Squeeze Transform and Palette, both inverse
and forward.

In the Encoding folder there are predictors using neighbors, averages,
weighted prediction and tree-based prediction.
