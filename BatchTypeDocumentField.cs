[DebuggerDisplay("{Name}")]
    public sealed class BatchTypeDocumentField : TenancyAndTrackOnly, INormalizable,
        IDataTypeValidator, INotNullValidator, INotBlankValidator,
        IMinimumValidator, IMaximumValidator, ILessThanValidator, IGreaterThanValidator,
        IMatchValidator
    {
        private const int RandomStringLength = 8;
        private string _name;
        private DataType _dataType;
        private DocumentField _documentField;
        private ICollection<BatchTypeDocumentFieldColumn> _tableColumns;
        private CustomizedName _customizedName;
        private CustomizedName CustomizedName => _customizedName ??= (CustomizedName)_name;

        public BatchTypeDocumentField() { }

        private BatchTypeDocumentField(Action<object, string> lazyLoader)
        {
            LazyLoader = lazyLoader;
        }

        private Action<object, string> LazyLoader { get; }

        public Guid BatchTypeId { get; set; }
        public Guid DocumentTypeId { get; set; }
        public Guid DocumentFieldId { get; set; }
        public string DisplayName { get; set; }
        public string NormalizedName { get; private set; }
        public FieldAttribute Requirement { get; set; }
        public bool Readonly { get; set; }
        public bool DataConfirmation { get; set; }
        public bool DoubleBlindKeying { get; set; }
        public bool RedactField { get; set; }
        public bool AlwaysValid { get; set; }
        public int? ConfidenceThreshold { get; set; }
        public DataValue DefaultValue { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string Expressions { get; set; }
        public DocumentFieldStatus Status { get; set; }
        public FieldFormat FieldFormat { get; set; }
        public DocumentLayoutField DocumentLayoutField { get; set; }
        public BatchType BatchType { get; set; }
        public DocumentType DocumentType { get; set; }
        public BatchTypeDocument BatchTypeDocument { get; set; }
        public DataSource DataSource { get; set; }
        public DocumentField DocumentField
        {
            get => LazyLoader.Load(this, ref _documentField);
            set => _documentField = value;
        }
        public int? TabOrder { get; set; }
        public string Name
        {
#pragma warning disable S4275 // Getters and setters should access the expected fields
            get => CustomizedName.Name;
            set
            {
                CustomizedName.Name = value;
                _name = CustomizedName;
            }
#pragma warning restore S4275 // Getters and setters should access the expected fields
        }

        public bool IsCustomized
        {
            get => CustomizedName.IsCustomized;
            set
            {
                CustomizedName.IsCustomized = value;
                _name = CustomizedName;
            }
        }

        public IPatternCollection TransformPatterns { get; set; }
        public IRule ValidationConditions { get; set; }
        public ICollection<BatchTypeDocumentFieldColumn> TableColumns
        {
            get => LazyLoader.Load(this, ref _tableColumns);
            set => _tableColumns = value;
        }

        public void Normalize() => NormalizedName
            = (Name + '_' + Generate(RandomStringLength, 0, requireLowercase: false, requireNonAlphanumeric: false)).Normalize().ToUpperInvariant();

        [NotMapped] DataType IDataTypeValidator.Type => DocumentField != null ? DocumentField.Type : LazyLoader.Load(this, ref _dataType);
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

    public enum DocumentFieldStatus
    {
        [Inactive]
        Inactive,

        [Active]
        Active,

        [Suspended]
        Suspended,

        [Deleted]
        Deleted
    }
    public enum FieldFormat
    {
        [EnumMember(Value = "short")]
        Short,
        [EnumMember(Value = "long")]
        Long,
        [EnumMember(Value = "longFit")]
        LongFit
    }