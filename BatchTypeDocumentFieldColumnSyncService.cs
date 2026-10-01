src\.Api\Api\Services\BatchTypeDocumentFieldColumnMapper.cs

using Microsoft.EntityFrameworkCore;
using Api.Models;
using Data;
using Data.EF;

namespace Api.Services
{
    public class BatchTypeDocumentFieldColumnSyncService
    {
        private readonly DbContext _dbContext;

        public BatchTypeDocumentFieldColumnSyncService(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SyncAsync(BatchTypeDocumentField field, DocumentFieldDto dto)
        {
            if (field == null || dto?.TableColumns == null)
            {
                return;
            }

            var existing = await _dbContext.BatchTypeDocumentFieldColumns
                .Where(c => c.BatchTypeId == field.BatchTypeId
                    && c.DocumentTypeId == field.DocumentTypeId
                    && c.DocumentFieldId == field.DocumentFieldId)
                .ToListAsync();

            var incoming = BatchTypeDocumentFieldColumnMapper.Create(dto, existing) ?? new List<BatchTypeDocumentFieldColumn>();
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
                item.ColumnDataType = source.ColumnDataType;
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
    }
}
