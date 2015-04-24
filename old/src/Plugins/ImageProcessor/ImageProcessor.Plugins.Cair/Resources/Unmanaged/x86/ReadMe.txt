User's ReadMe v2.18

CAIR - Content Aware Image Resizer
Copyright (C) 2008 Joseph Auman (brain.recall@gmail.com)

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+

CAIR is an image resizing library that is based on the works of Shai Avidan and 
Ariel Shamir.  It is a high-performance multi-threaded library written in C++. 
It is intended to be multi-platform and multi-architecture.


I am looking into implementing Poisson image reconstruction, which is mentioned 
in the paper, to be used in CAIR_HD(). See the paper on the subject here: 
http://research.microsoft.com/vision/cambridge/papers/perez_siggraph03.pdf 
However, I’m having a bit of difficulty understanding the method and how I can 
apply it to seam removal. If you know something about digital image processing 
and are willing to help, please email me at brain.recall@gmail.com


Compiling CAIR is not difficult. A Makefile for Linux is included to demonstrate.
Compiling under Visual Studio is a bit different, since the pthread DLL library
and object library must be included for the linker. I suggest Google to see how
it's done. Whenever possible, I *highly* suggest using the Intel C++ compiler, 
which gives about a 10% speed boost when all the optimization options are 
turned on. It's freely available for the Linux platform, but Windows and Mac
license are in the $600 range outside of the 30 day trial.

+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+

In this file I'm going to present each file and its functions. This is going to
targeted at "users" of the library (those actually making a GUI with it). Further
detail can be gained by reading the source code (don't worry, I take *some* pride
in my comments, but pthread experience would be helpful).


CAIR_CML.h
=================================================================================
The CAIR Matrix Library. A template class used to hold the image information 
in CAIR. The CML requires a size when creating the object. See main.cpp for
some examples in declaring and interfacing with the CML_Matrix.

- Depends on: nothing outside of the STL

- Types defined:
-- CML_byte - An unsigned char used for each color channel.
-- CML_RGBA - Structure for a 32 bit color pixel.
            - Each channel is a CML_byte, named as: red, green, blue, alpha
-- CML_color - A color matrix, replaces CML_Matrix<CML_RGBA>; use for images.
-- CML_int - An integer matrix, replaces CML_Matrix<int>; use for weights.

- Methods (the important ones, at least):
-- CML_Matrix( int x, int y )
--- Simple constructor. Requires the intended size of the matrix (can be changed
    later, so dummy values of (1,1) could be used).
-- void Fill( T value )
--- Sets all elements of the matrix to the given value.
-- operator()( int x, int y )
--- Accessor and assignment methods, used to set and get matrix values.
--- These have no bounds checking.
-- int Width()
--- Returns the width of the matrix.
-- int Height()
--- Returns the height of the matrix.
-- void Transpose( CML_Matrix<T> * Source )
--- Rotates Source on edge, storing the result into the matrix.
-- T Get( int x, int y )
--- Accessor function with full bounds checking. Out-of-bound accesses will be 
    constrained back into the matrix.
-- void D_Resize( int x, int y )
--- Destructively resize the matrix to the given values.
-- void Resize_Width( int x )
--- Careful with this one. Performs non-destructive "resizing" but only in the x
    direction. Essentially only changes what Width() will report. Enlarging should
    be done only after a Reserve(), for performance reasons.
-- void Shift_Row( int x, int y, int shift )
--- Shift the elements of a row, starting the (x,y) element. Shift determines
    amount of shift and direction. Negative will shift left, positive for right.



CAIR.h
=================================================================================
The CAIR function declarations.

Depends on: CAIR_CML.h
Types defined:
-- CAIR_NUM_THREADS - The number of default threads that will be used for Grayscale, Edge, and Add/Remove operations.
-- CAIR_direction - An enumeration for CAIR_Removal() with the values of AUTO,
                    VERTICAL, and HORIZONTAL. AUTO lets CAIR_Remvoal() determine
                    the best direction to remove seams. VERTICAL and HORIZONTAL
                    force the function to remove from their respective directions.
-- CAIR_convolution - An enumeration for all CAIR operations. It defines the available
                      convolution kernels. They include:
                      Prewitt: An X-Y kernel for decent edge detection.
                      Sobel: Works very much like Prewitt.
                      V1: Only the X-component of the Prewitt. More of an object detector
                          than strictly edges. Works well under some cases.
                      V_SQUARE: The result of V1 squared. This provides some of the
                                best object detection, thus some of the best operation.
                      Laplacian: A second-order edge detector. Nothing spectacular.
-- CAIR_energy - An enumeration for all CAIR energy algorithms.
                 Backward: The traditional energy algorithm.
                 Forward: The new energy algorithm that determines future edge changes and tries
                          to redirect seams away from potential artifacts. Comes at a slight performance hit.

Functions:
- void CAIR_Threads( int thread_count )
-- thread_count: the number of threads that the Grayscale/Edge/Add/Remove operations should use. Minimum of two.

- bool CAIR( CML_color * Source,
             CML_int * S_Weights,
             int goal_x,
             int goal_y,
             CAIR_convolution conv,
             CAIR_energy ener,
             CML_int * D_Weights,
             CML_color * Dest,
             bool (*CAIR_callback)(float) )
-- Source: pointer to the source image
-- S_Weights: pointer to the weights of the image, must be the same size as Source
   The weights allow for linear protection and removal of desired portions of an
   image. The values of the weights should not exceed -2,000,000 to 2,000,000 to
   avoid integer overflows. Generally, I found a -10,000 to 10,000 range to be 
   adequate. Large values will protect pixel, while small values will make it
   more likely to be removed first. ALWAYS use negative values for removal. When
   no preference is given to a pixel, leave its value to zero. (I suggest Fill()'ing
   the weight matrix with 0 before use, since the memory is not initialized when
   allocated.)
-- goal_x: the desired width of the Dest image
-- goal_y: the desired height of the Dest image
-- conv: The possible convolution kernels to use. See the above discussion.
   It is important to note that if using V_SQUARE the weights must be at least an order
   of magnitude larger for similar operation. add_weight needs to be several orders of 
   magnitude larger to avoid some stretching. This is because V_SQUARE produces larger
   edge values, and thus large energy values.
-- ener: The possible energy algorithms to use. See the above discussion.
-- D_Weights: pointer to the Destination Weights (contents will be destroyed)
-- Dest: pointer to the Destination image (contents will be destroyed)
-- CAIR_callback: a function pointer to a status/callback system. The function is expected
   to take a float of the percent complete (from 0 to 1) and is to return a false if the
   resize is to be canceled, or true otherwise. If the resize is canceled, CAIR() will return
   a false, leaving Dest and D_Weights in an unknown state. Set to NULL if not used.
-- RETURNS: true if the resize ran to completion, false if it was canceled by CAIR_callback.

- void CAIR_Grayscale( CML_color * Source, CML_color * Dest )
-- Source: pointer to the source image
-- Dest: The grayscale of Source image. The gray value will be computed, then applied
   to the three color channels to give the grayscale equivalent.
- void CAIR_Edge( CML_color * Source, CAIR_convolution conv, CML_color * Dest )
-- Source: pointer to the source image
-- conv: The edge detection kernel.
-- Dest: The dge detection of the source image. Values larger than a CML_byte
   (255 in decimal) will be clipped down to 255.

- CAIR_V_Energy( CML_color * Source, CAIR_convolution conv, CAIR_energy ener, CML_color * Dest ) and CAIR_H_Energy( CML_color * Source, CAIR_convolution conv, CAIR_energy ener, CML_color * Dest )
-- Source: pointer to the source image
-- conv: The edge detection kernel.
-- ener: The energy algorithm.
-- Dest: The grayscale equivalent of the energy map of the source image. All values
   are scaled down realtive to the largest value. Weights are assumed all zero,
   since when they are not they dominate the image and produce uninteresting results.

- bool CAIR_Removal( CML_color * Source,
                     CML_int * S_Weights,
                     CAIR_direction choice,
                     int max_attempts,
                     CAIR_convolution conv,
                     CAIR_energy ener,
                     CML_int * D_Weights,
                     CML_color * Dest,
                     bool (*CAIR_callback)(float) )
-- EXPERIMENTAL
-- S_Weights: pointer to the given weights of the image
-- choice: How the algorithm will remove the seams. In AUTO mode, it will count the
   negative rows (for horizontal removal) and the negative columns (for vertical removal)
   and then removes the least amount in that direction. Other settings will cause it to 
   remove in thier respective directions. After the removal, it is expanded back out to
   its origional dimensions.
-- max_attempts: The number of retries the algorithm will do to remove remaining negative
   weights. Sometimes the algorithm may not remove everything in one pass, and this attempts
   to give the algorithm another chance at it. There are situations, however, where the
   algorithm will not be able to remove the requested portions due to other areas makred
   for protection with a high weight.
-- conv: The edge detection kernel.
-- ener: The energy algorithm.
-- D_Weights: pointer to the destination weights
-- Dest: pointer to the destination image
-- CAIR_callback: a function pointer to a status/callback system
-- RETURNS: true if the resize ran to completion, false if it was canceled by CAIR_callback.

- bool CAIR_HD( CML_color * Source,
                CML_int * S_Weights,
                int goal_x,
                int goal_y,
                CAIR_convolution conv,
                CAIR_energy ener,
                CML_int * D_Weights,
                CML_color * Dest,
                bool (*CAIR_callback)(float) )
-- See CAIR() for the same paramaters.
-- CAIR_HD() is designed for quality, not speed. At each itteration, CAIR_HD()
   determines which direction produces the least energy path for removal. It then
   removes that path. CAIR_HD() can enlarge, but currently employs standard CAIR()
   to perform it.

CAIR.cpp
=================================================================================
The CAIR function definitions. Nothing really important to the user, except its
dependencies.

Depends on: CAIR_CML.h, CAIR.h, pthread.h
Types defined: none visible
Functions: none visible



main.cpp
=================================================================================
An example application that uses CAIR (mostly for testing by me). Functions to
read the source on are BMP_to_CML() and CML_to_BMP() to get an example on how to
convert to and from the CML.

- Depends on: CAIR_CML.h, CAIR.h, EasyBMP.h
Types defined: none
Functions: nothing really important


+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
Content Amplification

This is described in the paper, but I'll mention it here as well. This method
allows an object in the image to appear larger. Do this as following:
1) Enlarge an image using a standard linear technique by about 10% or so (more
   than 25% might cause some artifacts).
2) Optional: Set a large weight for the desired object.
3) Seam carve the enlarged image back down to the origional size.

+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+


Other Links:
http://brain.recall.googlepages.com/cair
http://c-a-i-r.wiki.sourceforge.net/
http://sourceforge.net/projects/c-a-i-r/
http://code.google.com/p/seam-carving-gui/
http://www.faculty.idc.ac.il/arik/papers/imret.pdf
http://www.faculty.idc.ac.il/arik/papers/vidRet.pdf

Special Thanks:
Ariel Shamir
Shai  Avidan
Michael Rubinstein
Ramin Sabet
Brett Taylor
Gabe Rudy
Jean-Baptiste (Jib)
David Phillip Oster
Matt Newell
Klaus Nordby
Alexandre Prokoudine
Peter Berrington

Further questions on CAIR can be directed to the source code, or my email.

Sincerely,
Brain_ReCall aka Joe Auman
brain.recall@gmail.com
