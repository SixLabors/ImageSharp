namespace ImageProcessor.Imaging.Filters.ObjectDetection
{
    using System;
    using System.Xml.Serialization;

    /// <summary>
    ///   Haar Cascade Classifier Stage.
    /// </summary>
    /// 
    /// <remarks>
    ///   A Haar Cascade Classifier is composed of several stages. Each stage
    ///   contains a set of classifier trees used in the decision process.
    /// </remarks>
    /// 
    [Serializable]
    [XmlRoot("_")]
    public class HaarCascadeStage : ICloneable
    {
        /// <summary>
        ///   Gets or sets the feature trees and its respective
        ///   feature tree nodes which compose this stage.
        /// </summary>
        /// 
        [XmlArray("trees")]
        [XmlArrayItem("_")]
        [XmlArrayItem("_", NestingLevel = 1)]
        public HaarFeatureNode[][] Trees { get; set; }

        /// <summary>
        ///   Gets or sets the threshold associated with this stage,
        ///   i.e. the minimum value the classifiers should output
        ///   to decide if the image contains the object or not.
        /// </summary>
        /// 
        [XmlElement("stage_threshold")]
        public double Threshold { get; set; }

        /// <summary>
        ///   Gets the index of the parent stage from this stage.
        /// </summary>
        /// 
        [XmlElement("parent")]
        public int ParentIndex { get; set; }

        /// <summary>
        ///   Gets the index of the next stage from this stage.
        /// </summary>
        /// 
        [XmlElement("next")]
        public int NextIndex { get; set; }

        /// <summary>
        ///   Constructs a new Haar Cascade Stage.
        /// </summary>
        /// 
        public HaarCascadeStage()
        {
        }

        /// <summary>
        ///   Constructs a new Haar Cascade Stage.
        /// </summary>
        /// 
        public HaarCascadeStage(double threshold)
        {
            this.Threshold = threshold;
        }

        /// <summary>
        ///   Constructs a new Haar Cascade Stage.
        /// </summary>
        /// 
        public HaarCascadeStage(double threshold, int parentIndex, int nextIndex)
        {
            this.Threshold = threshold;
            this.ParentIndex = parentIndex;
            this.NextIndex = nextIndex;
        }

        /// <summary>
        ///   Classifies an image as having the searched object or not.
        /// </summary>
        /// 
        public bool Classify(FastBitmap image, int x, int y, double factor)
        {
            double value = 0;

            // For each feature in the feature tree of the current stage,
            foreach (HaarFeatureNode[] tree in Trees)
            {
                int current = 0;

                do
                {
                    // Get the feature node from the tree
                    HaarFeatureNode node = tree[current];

                    // Evaluate the node's feature
                    double sum = node.Feature.GetSum(image, x, y);

                    // And increase the value accumulator
                    if (sum < node.Threshold * factor)
                    {
                        value += node.LeftValue;
                        current = node.LeftNodeIndex;
                    }
                    else
                    {
                        value += node.RightValue;
                        current = node.RightNodeIndex;
                    }

                } while (current > 0);

                // Stop early if we have already surpassed the stage threshold value.
                //if (value > this.Threshold) return true;
            }

            // After we have evaluated the output for the
            //  current stage, we will check if the value
            //  is still lesser than the stage threshold. 
            if (value < this.Threshold)
            {
                // If it is, the stage has rejected the current
                // image and it doesn't contains our object.
                return false;
            }
            else
            {
                // The stage has accepted the current image
                return true;
            }
        }


        /// <summary>
        ///   Creates a new object that is a copy of the current instance.
        /// </summary>
        /// 
        /// <returns>
        ///   A new object that is a copy of this instance.
        /// </returns>
        /// 
        public object Clone()
        {
            HaarFeatureNode[][] newTrees = new HaarFeatureNode[Trees.Length][];

            for (int i = 0; i < newTrees.Length; i++)
            {
                HaarFeatureNode[] tree = Trees[i];
                HaarFeatureNode[] newTree = newTrees[i] =
                    new HaarFeatureNode[tree.Length];

                for (int j = 0; j < newTree.Length; j++)
                    newTree[j] = (HaarFeatureNode)tree[j].Clone();
            }

            HaarCascadeStage r = new HaarCascadeStage();
            r.NextIndex = NextIndex;
            r.ParentIndex = ParentIndex;
            r.Threshold = Threshold;
            r.Trees = newTrees;

            return r;
        }

    }
}
