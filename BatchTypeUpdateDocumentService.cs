public async Tas ApplyDocumentsAsync() {
	if (documentFieldDto.ValidationConditions != null)
{
    batchTypeDocumentField.ValidationConditions = documentFieldDto.ValidationConditions;
}

if (documentFieldDto.TableColumns != null)
{
    await SyncTableColumnsAsync(batchTypeDocumentField, documentFieldDto);
}

var batchTypeDocumentField = batchTypeDocument.DocumentFields
    .SingleOrDefault(f => f.DocumentFieldId.Equals(documentFieldId));

if (batchTypeDocumentField == null)
{
    batchTypeDocumentField = new BatchTypeDocumentField
    {
        TableColumns = CreateTableColumns(documentFieldDto)
    };
	
	
	else if (batchTypeDocumentField.Status == DocumentFieldStatus.Suspended)
{
    batchTypeDocumentField.ValidationConditions = documentFieldDto.ValidationConditions;
    await SyncTableColumnsAsync(batchTypeDocumentField, documentFieldDto);
    if (documentLayout == null)
    {
        _logger.LogWarning($"Document layput is null {DateTime.UtcNow}");
    }
	
	
	
	   ValidationConditions = documentFieldDto.ValidationConditions, 880
       ableColumns = CreateTableColumns(documentFieldDto)
}


private static ICollection<BatchTypeDocumentFieldColumn> CreateTableColumns(DocumentFieldDto documentFieldDto, Data.Types.TableType tableType = null)
        {
            tableType ??= documentFieldDto?.Type as Data.Types.TableType;
            if (tableType == null)
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

        private async Task SyncTableColumnsAsync(BatchTypeDocumentField field, DocumentFieldDto dto)
        {
            if (field?.DocumentField?.Type is not Data.Types.TableType)
            {
                return;
            }

            var existing = await _dbContext.BatchTypeDocumentFieldColumns
                .Where(c => c.BatchTypeId == field.BatchTypeId
                    && c.DocumentTypeId == field.DocumentTypeId
                    && c.DocumentFieldId == field.DocumentFieldId)
                .ToListAsync();

            if (dto.TableColumns == null)
            {
                return;
            }

            var runtimeTableType = dto?.Type as Data.Types.TableType ?? field.DocumentField?.Type as Data.Types.TableType;
            var incoming = CreateTableColumns(dto, runtimeTableType) ?? new List<BatchTypeDocumentFieldColumn>();
            var incomingByIndex = incoming.ToDictionary(c => c.ColumnIndex, c => c);

            foreach (var item in existing)
            {
                if (!incomingByIndex.TryGetValue(item.ColumnIndex, out var source))
                {
                    _dbContext.BatchTypeDocumentFieldColumns.Remove(item);
                    continue;
                }

                item.Status = DocumentFieldStatus.Active;
                item.ColumnName = source.ColumnName;
                item.Requirement = source.Requirement;
                item.MinLength = source.MinLength;
                item.MaxLength = source.MaxLength;
                item.Expressions = source.Expressions;
                item.DataValue = source.DataValue;
                item.TransformPatterns = source.TransformPatterns;
                item.ValidationConditions = source.ValidationConditions;
                incomingByIndex.Remove(item.ColumnIndex);
            }

            foreach (var source in incomingByIndex.Values)
            {
                source.BatchTypeId = field.BatchTypeId;
                source.DocumentTypeId = field.DocumentTypeId;
                source.DocumentFieldId = field.DocumentFieldId;
                source.Status = DocumentFieldStatus.Active;
                await _dbContext.BatchTypeDocumentFieldColumns.AddAsync(source);
            }
        }