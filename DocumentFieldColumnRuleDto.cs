public record DocumentFieldColumnRuleDto
{
    public string Name { get; set; }
    public DataType Type { get; set; }
    public DataValue DataValue { get; set; }
    public FieldAttribute? Requirement { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string Expressions { get; set; }
    public IPatternCollection TransformPatterns { get; set; }
    public IRule ValidationConditions { get; set; }
}