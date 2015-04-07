namespace ImageProcessor.Imaging.Filters.ObjectDetection
{
    /// <summary>
    ///   Object detector options for the search procedure.
    /// </summary>
    /// 
    public enum ObjectDetectorSearchMode
    {
        /// <summary>
        ///   Entire image will be scanned.
        /// </summary>
        /// 
        Default = 0,

        /// <summary>
        ///   Only a single object will be retrieved.
        /// </summary>
        /// 
        Single,

        /// <summary>
        ///   If a object has already been detected inside an area,
        ///   it will not be scanned twice for inner or overlapping
        ///   objects, saving computation time.
        /// </summary>
        /// 
        NoOverlap,

        /// <summary>
        ///   If several objects are located within one another, 
        ///   they will be averaged. Additionally, objects which
        ///   have not been detected sufficient times may be dropped
        ///   by setting <see cref="HaarObjectDetector.Suppression"/>.
        /// </summary>
        /// 
        Average,
    }
}