using Microsoft.EntityFrameworkCore;
using Models;
using Services.Interface;
using Ricoh.RDI.Data;
using Ricoh.RDI.Data.EF;
using Ricoh.RDI.Data.Types;

namespace Services
{
    public class BatchTypeDocumentFieldColumnSyncService : IBatchTypeDocumentFieldColumnSyncService
    {
        private readonly iDbContext _dbContext;

        public BatchTypeDocumentFieldColumnSyncService(iDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ICollection<BatchTypeDocumentFieldColumn> CreateTableColumns(DocumentFieldDto documentFieldDto)
            => CreateTableColumns(documentFieldDto, null);

        private static ICollection<BatchTypeDocumentFieldColumn> CreateTableColumns(DocumentFieldDto documentFieldDto, Guid? fallbackRowId)
        {
            if (!IsTableType(documentFieldDto))
            {
                return new List<BatchTypeDocumentFieldColumn>();
            }

            var sourceColumns = GetSourceColumns(documentFieldDto);
            if (sourceColumns.Count == 0)
            {
                return new List<BatchTypeDocumentFieldColumn>();
            }

            // All columns of a table field belong to the same row, so they share a single RowId.
            var rowId = ResolveRowId(sourceColumns, fallbackRowId);
            var columns = new List<BatchTypeDocumentFieldColumn>();
            foreach (var item in sourceColumns)
            {
                if (!int.TryParse(item.Key, out var columnIndex) || item.Value == null)
                {
                    continue;
                }
                var columnTable = new BatchTypeDocumentFieldColumn
                {
                    ColumnIndex = columnIndex,
                    ColumnName = item.Value.Name,
                    RowId = rowId,
                    DataType = item.Value.Type ?? DataType.Undefined,
                    DataValue = item.Value.DataValue,
                    MinLength = item.Value.MinLength < 0 ? null : item.Value.MinLength,
                    MaxLength = item.Value.MaxLength < 0 ? null : item.Value.MaxLength,
                    Expressions = item.Value.Expressions,
                    TransformPatterns = item.Value.TransformPatterns,
                    ValidationConditions = item.Value.ValidationConditions
                };
                columnTable.Normalize();
                columns.Add(columnTable);
            }
            return columns;
        }

        public async Task SyncTableColumnsAsync(BatchTypeDocumentField field, DocumentFieldDto dto)
        {
            if (field == null || !IsTableType(dto))
            {
                return;
            }

            var sourceColumns = GetSourceColumns(dto);
            if (sourceColumns.Count == 0)
            {
                return;
            }

            var existing = await _dbContext.BatchTypeDocumentFieldColumns
                .Where(c => c.BatchTypeId == field.BatchTypeId
                    && c.DocumentTypeId == field.DocumentTypeId
                    && c.DocumentFieldId == field.DocumentFieldId
                    && c.DocumentId == Guid.Empty)
                .ToListAsync();

            // RowId is part of the key: existing definitions keep their RowId.
            var existingRowId = existing.Select(c => (Guid?)c.RowId).FirstOrDefault(id => id != Guid.Empty);
            var incoming = CreateTableColumns(dto, existingRowId);
            var incomingByIndex = incoming.ToDictionary(c => c.ColumnIndex, c => c);
            if (existingRowId.HasValue)
            {
                foreach (var column in incoming)
                {
                    column.RowId = existingRowId.Value;
                }
            }

            foreach (var item in existing)
            {
                if (!incomingByIndex.TryGetValue(item.ColumnIndex, out var source))
                {
                    _dbContext.BatchTypeDocumentFieldColumns.Remove(item);
                    continue;
                }

                item.Status = DocumentColumnStatus.Active;
                item.ColumnName = source.ColumnName;
                item.DataType = source.DataType;
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
                source.Status = DocumentColumnStatus.Active;
                await _dbContext.BatchTypeDocumentFieldColumns.AddAsync(source);
            }
        }

        private static Guid ResolveRowId(IDictionary<string, DocumentFieldColumnRuleDto> sourceColumns, Guid? fallbackRowId)
            => sourceColumns.Values.FirstOrDefault(c => c?.RowId is Id id && id != Guid.Empty)?.RowId
                ?? fallbackRowId
                ?? Guid.NewGuid();

        private static bool IsTableType(DocumentFieldDto dto)
            => dto?.Type is TableType;

        private static IDictionary<string, DocumentFieldColumnRuleDto> GetSourceColumns(DocumentFieldDto dto)
        {
            if (dto?.Columns != null && dto.Columns.Count > 0)
            {
                return dto.Columns;
            }

            if (dto?.Type is not TableType tableType)
            {
                return new Dictionary<string, DocumentFieldColumnRuleDto>();
            }

            var columns = new Dictionary<string, DocumentFieldColumnRuleDto>();
            var indexedColumns = tableType.GetColumns().Select((column, index) => new { column, index });
            foreach (var item in indexedColumns)
            {
                columns[item.index.ToString()] = new DocumentFieldColumnRuleDto
                {
                    Name = item.column.Name,
                    Type = item.column.Type
                };
            }

            return columns;
        }
    }
}
