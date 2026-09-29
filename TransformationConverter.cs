internal sealed partial class TransformationConverter
    {
        private static readonly IDictionary<string, Type> CoreTransformKinds = new[]
        {
            typeof(Callback.Format),
            typeof(Expression),
            typeof(Setter),
            typeof(SetContext),
            typeof(SetTableColumnContext)
        }
            .Select(t =>
            {
                var attr = t.GetCustomAttribute<KindAttribute>();
                return new { Type = t, Attribute = attr };
            })
            .Where(x => x.Attribute != null)
            .ToDictionary(x => x.Attribute.Name, x => x.Type);

        private static readonly IDictionary<string, Type> TransformKinds;

        static TransformationConverter()
        {
            TransformKinds = EvaluationTransformKinds
                .Concat(FormattingTransformKinds)
                .Concat(SubstitutionsTransformKinds)
                .Concat(CoreTransformKinds)
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }
