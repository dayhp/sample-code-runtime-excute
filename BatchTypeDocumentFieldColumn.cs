public sealed class BatchTypeDocumentFieldColumn : TenancyAndTrackOnly,
        IDataTypeValidator, INotNullValidator, INotBlankValidator,
        IMinimumValidator, IMaximumValidator, ILessThanValidator, IGreaterThanValidator,
        IMatchValidator
    {
        private BatchTypeDocumentField _documentField;

        public BatchTypeDocumentFieldColumn() { }

        private BatchTypeDocumentFieldColumn(Action<object, string> lazyLoader)
        {
            LazyLoader = lazyLoader;
        }

        private Action<object, string> LazyLoader { get; }

        public Guid BatchTypeId { get; set; }
        public Guid DocumentTypeId { get; set; }
        public Guid DocumentFieldId { get; set; }
        public int ColumnIndex { get; set; }
        public string ColumnName { get; set; }
        public DataType ColumnDataType { get; set; }
        public FieldAttribute Requirement { get; set; } = FieldAttribute.Optional;
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string Expressions { get; set; }
        public DataValue DataValue { get; set; }
        public DocumentFieldStatus Status { get; set; }
        public IPatternCollection TransformPatterns { get; set; }
        public IRule ValidationConditions { get; set; }

        public BatchTypeDocumentField DocumentField
        {
            get => LazyLoader.Load(this, ref _documentField);
            set => _documentField = value;
        }

        [NotMapped]
        DataType IDataTypeValidator.Type => ColumnDataType ?? DataType.Undefined;

        [NotMapped] bool INotNullValidator.IsNotNull => Requirement == FieldAttribute.Required;
        [NotMapped] bool INotBlankValidator.IsNotBlank => Requirement == FieldAttribute.Required;
        [NotMapped] int IMinimumValidator.Value => Math.Max(MinLength ?? -1, -1);
        [NotMapped] int IMaximumValidator.Value => Math.Max(MaxLength ?? -1, -1);
        [NotMapped] long? ILessThanValidator.Value => MaxLength;
        [NotMapped] long? IGreaterThanValidator.Value => MinLength;

        [NotMapped]
        Regex IMatchValidator.Regex
        {
            get
            {
                if (string.IsNullOrEmpty(Expressions))
                {
                    return null;
                }

                try
                {
                    return new Regex(Expressions);
                }
                catch
                {
                    return null;
                }
            }
        }
    }