\Code\src\Data\Patterns\SetTableColumnContext.cs
public partial record Transformation
    {
        [Kind("$set-table-column-context")]
        internal sealed record SetTableColumnContext : ITransformation
        {
            public SetTableColumnContext(IPattern target)
            {
                Target = target;
            }

            public IPattern Target { get; }

            public object Transform(IPatternContext context, IEnumerable<object> args)
            {
                var value = context.Object switch
                {
                    DataValue v => v,
                    IValue v => v.Value.ToValue(),
                    _ => context.Object.ToValue()
                };

                if (value == null || value == DataValue.Undefined())
                {
                    return context.Object;
                }

                if (Target is not Objects.DocumentObject documentObject)
                {
                    return context.Object;
                }

                var source = context.Context.Object switch
                {
                    Document d => d,
                    IHasDocumentObject hasDocument => hasDocument.Document,
                    _ => null
                };

                if (source == null || source.BatchTypeDocument == null)
                {
                    return context.Object;
                }

                // Expected path shape: fields['TableFieldName'].columns['ColumnName']
                var match = System.Text.RegularExpressions.Regex.Match(
                    documentObject.Path ?? string.Empty,
                    "^fields\\['(?<field>[^']+)'\\]\\.columns\\['(?<column>[^']+)'\\](?:\\.value)?$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    return context.Object;
                }

                var fieldName = match.Groups["field"].Value;
                var columnName = match.Groups["column"].Value;

                var targetField = source.BatchTypeDocument.DocumentFields?
                    .FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                        && f.Status < DocumentFieldStatus.Deleted);

                if (targetField?.DocumentField == null)
                {
                    return context.Object;
                }

                using var scope = context.GetRequiredService<ILifetimeScope>().BeginLifetimeScope();
                using var dbContext = scope.Resolve<DbContext>();

                var column = dbContext.BatchTypeDocumentFieldColumns
                    .FirstOrDefault(c => c.BatchTypeId == source.BatchTypeId
                        && c.DocumentTypeId == source.DocumentTypeId
                        && c.DocumentFieldId == targetField.DocumentFieldId
                        && c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)
                        && c.Status < DocumentFieldStatus.Deleted);

                if (column == null)
                {
                    return context.Object;
                }

                var dataValue = value;
                if (column.ColumnDataType != null && column.ColumnDataType != DataType.Undefined)
                {
                    if (!dataValue.IsType(column.ColumnDataType)
                        && dataValue is not ListValue && column.ColumnDataType is not ListType
                        && dataValue is not TableValue && column.ColumnDataType is not TableType)
                    {
                        dataValue = dataValue.ConvertTo(column.ColumnDataType).ToDataValue();
                    }

                    if (column.ColumnDataType is ListType listType && dataValue.IsType(column.ColumnDataType))
                    {
                        dataValue = dataValue.FormatListFieldValue(listType);
                    }

                    if (column.ColumnDataType is TableType tableType && dataValue.IsType(column.ColumnDataType))
                    {
                        dataValue = dataValue.FormatTableFieldValue(tableType);
                    }
                }

                column.DataValue = dataValue;
                dbContext.SaveChanges();
                return context.Object;
            }
        }