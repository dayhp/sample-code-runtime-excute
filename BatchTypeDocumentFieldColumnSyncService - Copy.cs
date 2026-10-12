using Autofac;
using Microsoft.EntityFrameworkCore;
using Services.Interface;
using Data;
using Data.EF;
using Data.Patterns;
using Data.Types;

namespace Services
{
    /// <summary>
    /// Handles document table column transforms/validations and writes table values back to field values.
    /// </summary>
    public class DocumentTableColumnValueService : IDocumentTableColumnValueService
    {
        private const string RowIdKey = "rowId";
        private readonly DbContext _dbContext;
        private readonly IPatternResolver _patternResolver;
        private readonly IBatchFieldValueService _fieldValueService;
        private readonly ILifetimeScope _lifetimeScope;

        public DocumentTableColumnValueService(
            DbContext dbContext,
            IPatternResolver patternResolver,
            IBatchFieldValueService fieldValueService,
            ILifetimeScope lifetimeScope)
        {
            _dbContext = dbContext;
            _patternResolver = patternResolver;
            _fieldValueService = fieldValueService;
            _lifetimeScope = lifetimeScope;
        }

        public IReadOnlyList<Id?> DetachRowIds(DataValue tableValue, out DataValue tableValueWithoutRowIds)
        {
            tableValueWithoutRowIds = tableValue;
            if (tableValue is not IListValue listValue)
            {
                return Array.Empty<Id?>();
            }

            var rowIds = new List<Id?>();
            var rows = new List<IDictionary<string, DataValue>>();
            foreach (var row in listValue.AsEnumerable().OfType<IDictionary<string, DataValue>>())
            {
                var rowId = TryGetRowId(row);
                rowIds.Add(rowId.HasValue ? new Id(rowId.Value) : null);
                rows.Add(row
                    .Where(c => !string.Equals(c.Key, RowIdKey, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(c => c.Key, c => c.Value));
            }

            tableValueWithoutRowIds = TableType.From(rows);
            return rowIds;
        }

        public DataValue EnsureRowIds(DataValue tableValue)
        {
            if (tableValue is not IListValue listValue)
            {
                return tableValue;
            }

            var rows = new List<IDictionary<string, DataValue>>();
            foreach (var row in listValue.AsEnumerable().OfType<IDictionary<string, DataValue>>())
            {
                if (TryGetRowId(row).HasValue)
                {
                    rows.Add(row);
                    continue;
                }

                rows.Add(new Dictionary<string, DataValue>(row.Where(c => !string.Equals(c.Key, RowIdKey, StringComparison.OrdinalIgnoreCase)))
                {
                    [RowIdKey] = TextType.From(Guid.NewGuid().ToString())
                });
            }

            return TableType.From(rows);
        }

        public async Task<DataValue> SyncTableRowColumnsAsync(Document doc, BatchTypeDocumentField currentField, DataValue tableValue, DataValue savedValue)
        {
            tableValue = EnsureRowIds(ApplySavedRowIds(tableValue, savedValue));
            if (doc == null || currentField == null)
            {
                return tableValue;
            }

            var definitions = await _dbContext.BatchTypeDocumentFieldColumns
                .AsNoTracking()
                .Where(c => c.BatchTypeId == currentField.BatchTypeId
                    && c.DocumentTypeId == currentField.DocumentTypeId
                    && c.DocumentFieldId == currentField.DocumentFieldId
                    && c.DocumentId == Guid.Empty
                    && c.Status == DocumentColumnStatus.Active)
                .OrderBy(c => c.ColumnIndex)
                .ToListAsync();

            if (definitions.Count == 0)
            {
                return tableValue;
            }

            // Deleted cells are loaded too: a row sent again with the same rowId is restored (same key).
            await _dbContext.BatchTypeDocumentFieldColumns
                .IgnoreQueryFilters()
                .Where(c => c.BatchTypeId == currentField.BatchTypeId
                    && c.DocumentTypeId == currentField.DocumentTypeId
                    && c.DocumentFieldId == currentField.DocumentFieldId
                    && c.DocumentId == doc.Id)
                .LoadAsync();

            // Taken from the tracked cells (loaded above + added earlier in the request, which a query does not
            // return) so a cell already tracked is never added twice.
            var cells = _dbContext.BatchTypeDocumentFieldColumns.Local
                .Where(c => c.BatchTypeId == currentField.BatchTypeId
                    && c.DocumentTypeId == currentField.DocumentTypeId
                    && c.DocumentFieldId == currentField.DocumentFieldId
                    && c.DocumentId == doc.Id)
                .ToList();
            var cellsByKey = cells
                .GroupBy(c => (c.RowId, c.ColumnIndex))
                .ToDictionary(g => g.Key, g => g.First());
            var syncedKeys = new HashSet<(Guid RowId, int ColumnIndex)>();

            var rows = tableValue is IListValue listValue
                ? listValue.AsEnumerable().OfType<IDictionary<string, DataValue>>()
                : Enumerable.Empty<IDictionary<string, DataValue>>();
            foreach (var row in rows)
            {
                if (TryGetRowId(row) is not Guid rowId || rowId == Guid.Empty)
                {
                    continue;
                }

                foreach (var definition in definitions)
                {
                    var key = (rowId, definition.ColumnIndex);

                    // Same rowId sent twice (e.g. a copied row): only the first row is saved.
                    if (!syncedKeys.Add(key))
                    {
                        continue;
                    }

                    var cellValue = row.TryGetValue(definition.ColumnIndex.ToString(), out var value) && value != null
                        ? value
                        : DataValue.Undefined();

                    if (cellsByKey.TryGetValue(key, out var cell))
                    {
                        // Updated row (or restored deleted row).
                        cell.Status = DocumentColumnStatus.Active;
                        cell.ColumnName = definition.ColumnName;
                        cell.DataType = definition.DataType;
                        if (cell.DataValue != cellValue)
                        {
                            cell.DataValue = cellValue;
                        }

                        continue;
                    }

                    // New row.
                    var newCell = new BatchTypeDocumentFieldColumn
                    {
                        BatchTypeId = definition.BatchTypeId,
                        DocumentTypeId = definition.DocumentTypeId,
                        DocumentFieldId = definition.DocumentFieldId,
                        DocumentId = doc.Id,
                        RowId = rowId,
                        ColumnIndex = definition.ColumnIndex,
                        ColumnName = definition.ColumnName,
                        DataType = definition.DataType,
                        DataValue = cellValue,
                        MinLength = definition.MinLength,
                        MaxLength = definition.MaxLength,
                        Expressions = definition.Expressions,
                        FieldFormat = definition.FieldFormat,
                        Status = DocumentColumnStatus.Active,
                        TenantId = definition.TenantId
                    };
                    newCell.Normalize();
                    await _dbContext.BatchTypeDocumentFieldColumns.AddAsync(newCell);
                }
            }

            // Deleted rows (and cells of columns no longer active).
            foreach (var cell in cells.Where(c => c.Status < DocumentColumnStatus.Deleted && !syncedKeys.Contains((c.RowId, c.ColumnIndex))))
            {
                cell.Status = DocumentColumnStatus.Deleted;
            }

            return tableValue;
        }

        public DataValue MergeResolvedColumnValues(
            Document doc, BatchTypeDocumentField currentField, IReadOnlyList<BatchTypeDocumentFieldColumn> columns,
            DataValue inputTableValue, IReadOnlyList<Id?> inputRowIds, out bool isResolved)
        {
            isResolved = false;
            if (doc == null || currentField == null || columns == null)
            {
                return inputTableValue ?? DataValue.Undefined();
            }

            var rows = inputTableValue is IListValue inputList
                ? inputList.AsEnumerable()
                    .OfType<IDictionary<string, DataValue>>()
                    .Select(r => (IDictionary<string, DataValue>)new Dictionary<string, DataValue>(r))
                    .ToList()
                : new List<IDictionary<string, DataValue>>();
            inputRowIds ??= Array.Empty<Id?>();

            var transformColumns = columns
                .Where(c => c.TransformPatterns != null && c.TransformPatterns.Count > 0)
                .ToList();

            // An empty table gets one new row so the column transforms can fill it.
            if (rows.Count == 0 && transformColumns.Count > 0)
            {
                rows.Add(new Dictionary<string, DataValue>());
            }

            isResolved = true;
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                foreach (var column in columns)
                {
                    row.TryAdd(column.ColumnIndex.ToString(), DataValue.Undefined());
                }

                var rowId = (rowIndex < inputRowIds.Count ? inputRowIds[rowIndex] : null) ?? Id.NewGuid();
                var tableRow = ResolveRow(doc, currentField, rowId, columns, transformColumns, row);
                isResolved &= transformColumns.All(c => tableRow.SavedColumnIndexes.Contains(c.ColumnIndex));

                // Added after the transforms: identifies the row in the column values (new rows get their id here).
                row[RowIdKey] = TextType.From(((Guid)rowId).ToString());
            }

            return TableType.From(rows);
        }

        public void ResolveAndEnsureTableColumnValue(Document doc, BatchTypeDocumentField currentField, int columnIndex)
        {
            throw new NotImplementedException();
        }

        public void ResolveAndEnsureTableValue(Document doc, BatchTypeDocumentField currentField)
        {
            throw new NotImplementedException();
        }

        public async Task SaveTableValueAsync(Document doc, BatchTypeDocumentField currentField, DataValue tableValue)
        {
            if (doc == null || currentField == null || tableValue == null)
            {
                return;
            }

            // Saved through its own DbContext: the request DbContext holds the payload values put in the document
            // context, which must not be saved.
            await using var scope = _lifetimeScope.BeginLifetimeScope();
            await using var dbContext = scope.Resolve<DbContext>();

            var fieldValues = dbContext.Set<DocumentFieldValue>();
            var fieldValue = await fieldValues.FindAsync(doc.Id, doc.BatchTypeId, doc.DocumentTypeId, currentField.NormalizedName);
            if (fieldValue == null)
            {
                fieldValue = new DocumentFieldValue(currentField)
                {
                    DocumentId = doc.Id
                };
                fieldValues.Add(fieldValue);
            }

            fieldValue.Value = EnsureRowIds(tableValue);
            await dbContext.SaveChangesAsync();
        }

        #region  Private method
        /// <summary>
        /// Runs the column transforms for one row: cells of the row are readable as parameters
        /// (<see cref="TableRowPatternContext.CellParameter"/>) and <c>$set-column-context</c> writes each value into
        /// its cell (nothing is saved to the database here).
        /// </summary>
        /// <returns>The resolved row, with the indexes of the columns set by the transforms.</returns>
        private TableRowPatternContext ResolveRow(
            Document doc,
            BatchTypeDocumentField currentField,
            Guid rowId,
            IReadOnlyCollection<BatchTypeDocumentFieldColumn> columns,
            IReadOnlyCollection<BatchTypeDocumentFieldColumn> transformColumns,
            IDictionary<string, DataValue> row)
        {
            var context = _patternResolver.CreateContext(doc);
            var tableRow = new TableRowPatternContext(currentField.DocumentFieldId, rowId, columns, row);
            context.Items[TableRowPatternContext.ItemKey] = tableRow;

            foreach (var column in transformColumns)
            {
                // Refreshed before each transform so a column can read cells written by the previous ones.
                foreach (var cellColumn in columns)
                {
                    context.SetParameter(TableRowPatternContext.CellParameter(cellColumn.ColumnName), row[cellColumn.ColumnIndex.ToString()]);
                }

                context.Items[TableRowPatternContext.ColumnItemKey] = column;
                context.Resolve(column.TransformPatterns);
            }

            // Cells are not validated here; validation is done by the field validation API.
            return tableRow;
        }

        /// <summary>
        /// Rows sent without `rowId` take the `rowId` of the saved row at the same index (not already used by
        /// another row), so the client does not have to send it back: the row is updated instead of added.
        /// </summary>
        private DataValue ApplySavedRowIds(DataValue tableValue, DataValue savedValue)
        {
            if (tableValue is not IListValue listValue)
            {
                return tableValue;
            }

            var savedRowIds = DetachRowIds(savedValue ?? DataValue.Undefined(), out _);
            var inputRows = listValue.AsEnumerable().OfType<IDictionary<string, DataValue>>().ToList();
            var usedRowIds = inputRows.Select(TryGetRowId).Where(id => id.HasValue).Select(id => id.Value).ToHashSet();

            var rows = new List<IDictionary<string, DataValue>>();
            for (var rowIndex = 0; rowIndex < inputRows.Count; rowIndex++)
            {
                var row = inputRows[rowIndex];
                var savedRowId = rowIndex < savedRowIds.Count ? savedRowIds[rowIndex] : null;
                if (TryGetRowId(row).HasValue || savedRowId is not Id rowId || !usedRowIds.Add(rowId))
                {
                    rows.Add(row);
                    continue;
                }

                rows.Add(new Dictionary<string, DataValue>(row.Where(c => !string.Equals(c.Key, RowIdKey, StringComparison.OrdinalIgnoreCase)))
                {
                    [RowIdKey] = TextType.From(((Guid)rowId).ToString())
                });
            }

            return TableType.From(rows);
        }

        private static Guid? TryGetRowId(IDictionary<string, DataValue> row)
        {
            var rowIdKey = row.Keys.FirstOrDefault(k => string.Equals(k, RowIdKey, StringComparison.OrdinalIgnoreCase));
            return rowIdKey != null && Id.TryParse(row[rowIdKey]?.Value?.ToString(), out Id rowId) && rowId != Id.Empty
                ? rowId
                : null;
        }
        #endregion
    }
}
