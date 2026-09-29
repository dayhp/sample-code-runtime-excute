ValidationConditions = documentFieldDto.ValidationConditions 117
TableColumns = CreateTableColumns(documentFieldDto)



{
            if (documentFieldDto?.Type is not TableType tableType)
            {
                return null;
            }

            if (documentFieldDto.TableColumns == null || documentFieldDto.TableColumns.Count == 0)
            {
                return null;
            }

            var columns = new List<BatchTypeDocumentFieldColumn>();
            foreach (var item in documentFieldDto.TableColumns)
            {
                if (!int.TryParse(item.Key, out var columnIndex) || item.Value == null)
                {
                    continue;
                }

                columns.Add(new BatchTypeDocumentFieldColumn
                {
                    ColumnIndex = columnIndex,
                    ColumnName = item.Value.Name,
                    ColumnDataType = item.Value.Type ?? tableType.GetColumns().ElementAtOrDefault(columnIndex).Type ?? DataType.Undefined,
                    DataValue = item.Value.DataValue,
                    Requirement = item.Value.Requirement ?? FieldAttribute.Optional,
                    MinLength = item.Value.MinLength < 0 ? null : item.Value.MinLength,
                    MaxLength = item.Value.MaxLength < 0 ? null : item.Value.MaxLength,
                    Expressions = item.Value.Expressions,
                    TransformPatterns = item.Value.TransformPatterns,
                    ValidationConditions = item.Value.ValidationConditions
                });
            }

            return columns;
        }