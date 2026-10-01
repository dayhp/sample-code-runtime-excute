using Api.Models;
using Data;
using DataType = Data.Types.DataType;

namespace Api.Services
{
    internal static class BatchTypeDocumentFieldColumnMapper
    {
        public static ICollection<BatchTypeDocumentFieldColumn> Create(
            DocumentFieldDto documentFieldDto,
            IReadOnlyCollection<BatchTypeDocumentFieldColumn> existingColumns = null)
        {
            if (documentFieldDto?.TableColumns == null || documentFieldDto.TableColumns.Count == 0)
            {
                return null;
            }

            var columns = new List<BatchTypeDocumentFieldColumn>();
            var usedIndexes = new HashSet<int>();
            var fallbackIndex = 0;
            var existingByName = existingColumns?
                .Where(x => !string.IsNullOrWhiteSpace(x.ColumnName))
                .GroupBy(x => x.ColumnName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, BatchTypeDocumentFieldColumn>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in documentFieldDto.TableColumns)
            {
                if (item.Value == null)
                {
                    continue;
                }

                var (resolved, columnIndex, columnName, columnDataType) = ResolveColumn(
                    item.Key,
                    existingByName,
                    usedIndexes,
                    ref fallbackIndex);

                if (!resolved)
                {
                    continue;
                }

                columns.Add(new BatchTypeDocumentFieldColumn
                {
                    ColumnIndex = columnIndex,
                    ColumnName = item.Value.Name ?? columnName ?? item.Key,
                    ColumnDataType = item.Value.Type ?? columnDataType ?? DataType.Undefined,
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

        private static (bool Resolved, int ColumnIndex, string ColumnName, DataType ColumnDataType) ResolveColumn(
            string key,
            IReadOnlyDictionary<string, BatchTypeDocumentFieldColumn> existingByName,
            HashSet<int> usedIndexes,
            ref int fallbackIndex)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return (false, default, null, null);
            }

            if (int.TryParse(key, out var columnIndex))
            {
                if (columnIndex < 0)
                {
                    return (false, default, null, null);
                }

                usedIndexes.Add(columnIndex);
                return (true, columnIndex, null, null);
            }

            if (existingByName.TryGetValue(key, out var existing))
            {
                usedIndexes.Add(existing.ColumnIndex);
                return (true, existing.ColumnIndex, existing.ColumnName, existing.ColumnDataType);
            }

            while (usedIndexes.Contains(fallbackIndex))
            {
                fallbackIndex++;
            }

            var allocatedIndex = fallbackIndex;
            usedIndexes.Add(allocatedIndex);
            fallbackIndex++;

            return (true, allocatedIndex, key, null);
        }
    }
}
