// Accord Vision Library
// The Accord.NET Framework (LGPL)
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2015
// cesarsouza at gmail.com
//
// Copyright © Masakazu Ohtsuka, 2008
//   This work is partially based on the original Project Marilena code,
//   distributed under a 2-clause BSD License. Details are listed below.
//
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
//   * Redistribution's of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.
//
//   * Redistribution's in binary form must reproduce the above copyright notice,
//     this list of conditions and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//
// This software is provided by the copyright holders and contributors "as is" and
// any express or implied warranties, including, but not limited to, the implied
// warranties of merchantability and fitness for a particular purpose are disclaimed.
// In no event shall the Intel Corporation or contributors be liable for any direct,
// indirect, incidental, special, exemplary, or consequential damages
// (including, but not limited to, procurement of substitute goods or services;
// loss of use, data, or profits; or business interruption) however caused
// and on any theory of liability, whether in contract, strict liability,
// or tort (including negligence or otherwise) arising in any way out of
// the use of this software, even if advised of the possibility of such damage.
//

namespace ImageProcessor.Imaging.Filters.ObjectDetection
{
    using System;
    using System.Drawing;

    /// <summary>
    ///   Strong classifier based on a weaker cascade of
    ///   classifiers using Haar-like rectangular features.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    ///   The Viola-Jones object detection framework is the first object detection framework
    ///   to provide competitive object detection rates in real-time proposed in 2001 by Paul
    ///   Viola and Michael Jones. Although it can be trained to detect a variety of object
    ///   classes, it was motivated primarily by the problem of face detection.</para>
    ///   
    /// <para>
    ///   The implementation of this code has used Viola and Jones' original publication, the
    ///   OpenCV Library and the Marilena Project as reference. OpenCV is released under a BSD
    ///   license, it is free for both academic and commercial use. Please be aware that some
    ///   particular versions of the Haar object detection framework are patented by Viola and
    ///   Jones and may be subject to restrictions for use in commercial applications. The code
    ///   has been implemented with full support for tilted Haar features.</para>
    ///   
    ///  <para>
    ///     References:
    ///     <list type="bullet">
    ///       <item><description>
    ///         <a href="http://www.cs.utexas.edu/~grauman/courses/spring2007/395T/papers/viola_cvpr2001.pdf">
    ///         Viola, P. and Jones, M. (2001). Rapid Object Detection using a Boosted Cascade
    ///         of Simple Features.</a></description></item>
    ///       <item><description>
    ///         <a href="http://en.wikipedia.org/wiki/Viola-Jones_object_detection_framework">
    ///         http://en.wikipedia.org/wiki/Viola-Jones_object_detection_framework</a>
    ///       </description></item>
    ///     </list>
    ///   </para>
    /// </remarks>
    /// 
    [Serializable]
    public class HaarClassifier
    {
        private HaarCascade cascade;

        private float invArea;
        private float scale;


        /// <summary>
        ///   Constructs a new classifier.
        /// </summary>
        /// 
        public HaarClassifier(HaarCascade cascade)
        {
            this.cascade = cascade;
        }

        /// <summary>
        ///   Constructs a new classifier.
        /// </summary>
        /// 
        public HaarClassifier(int baseWidth, int baseHeight, HaarCascadeStage[] stages)
            : this(new HaarCascade(baseWidth, baseHeight, stages)) { }


        /// <summary>
        ///   Gets the cascade of weak-classifiers
        ///   used by this strong classifier.
        /// </summary>
        /// 
        public HaarCascade Cascade
        {
            get { return cascade; }
        }

        /// <summary>
        ///   Gets or sets the scale of the search window
        ///   being currently used by the classifier.
        /// </summary>
        /// 
        public float Scale
        {
            get { return this.scale; }
            set
            {
                if (this.scale == value)
                    return;

                this.scale = value;
                this.invArea = 1f / (cascade.Width * cascade.Height * scale * scale);

                // For each stage in the cascade 
                foreach (HaarCascadeStage stage in cascade.Stages)
                {
                    // For each tree in the cascade
                    foreach (HaarFeatureNode[] tree in stage.Trees)
                    {
                        // For each feature node in the tree
                        foreach (HaarFeatureNode node in tree)
                        {
                            // Set the scale and weight for the node feature
                            node.Feature.SetScaleAndWeight(value, invArea);
                        }
                    }
                }
            }
        }


        /// <summary>
        ///   Detects the presence of an object in a given window.
        /// </summary>
        public bool Compute(FastBitmap image, Rectangle rectangle)
        {
            int x = rectangle.X;
            int y = rectangle.Y;
            int w = rectangle.Width;
            int h = rectangle.Height;

            double mean = image.GetSum(x, y, w, h) * invArea;
            double var = image.GetSum2(x, y, w, h) * invArea - (mean * mean);

            double sdev = (var >= 0) ? Math.Sqrt(var) : 1;

            // For each classification stage in the cascade
            foreach (HaarCascadeStage stage in cascade.Stages)
            {
                // Check if the stage has rejected the image
                if (stage.Classify(image, x, y, sdev) == false)
                {
                    return false; // The image has been rejected.
                }
            }

            // If the object has gone all stages and has not
            // been rejected, the object has been detected.
            return true; // The image has been detected.
        }
    }
}