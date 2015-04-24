namespace ImageProcessor.Imaging.Filters.ObjectDetection
{

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class opencv_storage
    {

        private opencv_storageCascade cascadeField;

        /// <remarks/>
        public opencv_storageCascade cascade
        {
            get
            {
                return this.cascadeField;
            }
            set
            {
                this.cascadeField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class opencv_storageCascade
    {

        private string stageTypeField;

        private string featureTypeField;

        private byte heightField;

        private byte widthField;

        private opencv_storageCascadeStageParams stageParamsField;

        private opencv_storageCascadeFeatureParams featureParamsField;

        private byte stageNumField;

        private opencv_storageCascade_[] stagesField;

        private opencv_storageCascade_2[] featuresField;

        private string type_idField;

        /// <remarks/>
        public string stageType
        {
            get
            {
                return this.stageTypeField;
            }
            set
            {
                this.stageTypeField = value;
            }
        }

        /// <remarks/>
        public string featureType
        {
            get
            {
                return this.featureTypeField;
            }
            set
            {
                this.featureTypeField = value;
            }
        }

        /// <remarks/>
        public byte height
        {
            get
            {
                return this.heightField;
            }
            set
            {
                this.heightField = value;
            }
        }

        /// <remarks/>
        public byte width
        {
            get
            {
                return this.widthField;
            }
            set
            {
                this.widthField = value;
            }
        }

        /// <remarks/>
        public opencv_storageCascadeStageParams stageParams
        {
            get
            {
                return this.stageParamsField;
            }
            set
            {
                this.stageParamsField = value;
            }
        }

        /// <remarks/>
        public opencv_storageCascadeFeatureParams featureParams
        {
            get
            {
                return this.featureParamsField;
            }
            set
            {
                this.featureParamsField = value;
            }
        }

        /// <remarks/>
        public byte stageNum
        {
            get
            {
                return this.stageNumField;
            }
            set
            {
                this.stageNumField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("_", IsNullable = false)]
        public opencv_storageCascade_[] stages
        {
            get
            {
                return this.stagesField;
            }
            set
            {
                this.stagesField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("_", IsNullable = false)]
        public opencv_storageCascade_2[] features
        {
            get
            {
                return this.featuresField;
            }
            set
            {
                this.featuresField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string type_id
        {
            get
            {
                return this.type_idField;
            }
            set
            {
                this.type_idField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class opencv_storageCascadeStageParams
    {

        private byte maxWeakCountField;

        /// <remarks/>
        public byte maxWeakCount
        {
            get
            {
                return this.maxWeakCountField;
            }
            set
            {
                this.maxWeakCountField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class opencv_storageCascadeFeatureParams
    {

        private byte maxCatCountField;

        /// <remarks/>
        public byte maxCatCount
        {
            get
            {
                return this.maxCatCountField;
            }
            set
            {
                this.maxCatCountField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class opencv_storageCascade_
    {

        private byte maxWeakCountField;

        private double stageThresholdField;

        private opencv_storageCascade__[] weakClassifiersField;

        /// <remarks/>
        public byte maxWeakCount
        {
            get
            {
                return this.maxWeakCountField;
            }
            set
            {
                this.maxWeakCountField = value;
            }
        }

        /// <remarks/>
        public double stageThreshold
        {
            get
            {
                return this.stageThresholdField;
            }
            set
            {
                this.stageThresholdField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("_", IsNullable = false)]
        public opencv_storageCascade__[] weakClassifiers
        {
            get
            {
                return this.weakClassifiersField;
            }
            set
            {
                this.weakClassifiersField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class opencv_storageCascade__
    {

        private string internalNodesField;

        private string leafValuesField;

        /// <remarks/>
        public string internalNodes
        {
            get
            {
                return this.internalNodesField;
            }
            set
            {
                this.internalNodesField = value;
            }
        }

        /// <remarks/>
        public string leafValues
        {
            get
            {
                return this.leafValuesField;
            }
            set
            {
                this.leafValuesField = value;
            }
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class opencv_storageCascade_2
    {

        private string[] rectsField;

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("_", IsNullable = false)]
        public string[] rects
        {
            get
            {
                return this.rectsField;
            }
            set
            {
                this.rectsField = value;
            }
        }
    }


}
