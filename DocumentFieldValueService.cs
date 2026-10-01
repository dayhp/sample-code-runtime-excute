using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Data;
using Data.EF;
using Data.Helper;
using Data.Patterns;
using Data.Types;

namespace Services
{
    /// <summary>
    /// Service responsible for inserting or updating document field values.
    /// </summary>
    internal sealed class DocumentFieldValueService
    {
        private readonly ILogger _logger;
        private readonly IPatternResolver _patternResolver;


        public DocumentFieldValueService(ILogger<DocumentFieldValueService> logger, IPatternResolver patternResolver)
        {
            _logger = logger;
            _patternResolver = patternResolver;
        }

        public async Task InsertOrUpdateDocumentFieldValuesAsync(
            ILifetimeScope scope,
            List<DocumentFieldValue> fieldValues,
            Document document,
            DbContext dbContext)
        {
            var listDataInsert = new List<DocumentFieldValue>();

            foreach (var item in fieldValues)
            {
                if (item.DocumentField == null)
                {
                    if (string.IsNullOrEmpty(item.FieldName))
                    {
                        continue;
                    }

                    var documentField = await dbContext.BatchTypeDocumentFields.FirstOrDefaultAsync(f => f.DocumentTypeId == item.DocumentTypeId && item.FieldName.Equals(f.NormalizedName));
                    if (documentField == null)
                    {
                        continue;
                    }

                    item.DocumentField = documentField;
                }

                if (item.DocumentField.TransformPatterns != null && item.DocumentField.TransformPatterns.Count > 0)
                {
                    FieldValueHelper.EnsureFieldValueExist(document, item.DocumentField, item.Value);
                    var newContext = _patternResolver.CreateContext(document, scope);
                    newContext.Resolve(item.DocumentField.TransformPatterns);
                    var resolved = newContext.Resolve(Data.Patterns.Builders.Objects.Document($"fields['{item.DocumentField.Name}'].value"));
                    item.Value = resolved.ToDataValue();
                }

                await ApplyTableColumnTransformPatternAsync(scope, dbContext, document, item);

                var fieldValue = document.FieldValues.FirstOrDefault(x => x.DocumentId == item.DocumentId && x.FieldName == item.FieldName);

                if (fieldValue != null)
                {
                    if (fieldValue.Value != item.Value)
                    {
                        fieldValue.Value = item.Value;
                        fieldValue.Confidence = item.Confidence;
                        fieldValue.Position = item.Position;
                    }
                }
                else
                {
                    listDataInsert.Add(item);
                }
            }

            if (listDataInsert.Count > 0)
            {
                await dbContext.AddRangeAsync(listDataInsert);
            }
        }

        private async Task ApplyTableColumnTransformPatternAsync(
            ILifetimeScope scope,
            DbContext dbContext,
            Document document,
            DocumentFieldValue item)
        {
            if (item?.DocumentField?.DocumentField?.Type is not TableType)
            {
                return;
            }

            var columns = await dbContext.BatchTypeDocumentFieldColumns
                .Where(c => c.BatchTypeId == item.BatchTypeId
                    && c.DocumentTypeId == item.DocumentTypeId
                    && c.DocumentFieldId == item.DocumentField.DocumentFieldId
                    && c.Status < DocumentFieldStatus.Deleted)
                .ToListAsync();

            if (columns.Count == 0)
            {
                return;
            }

            FieldValueHelper.EnsureFieldValueExist(document, item.DocumentField, item.Value);

            var context = _patternResolver.CreateContext(document, scope);
            foreach (var column in columns)
            {
                if (column.TransformPatterns == null || column.TransformPatterns.Count == 0)
                {
                    continue;
                }

                var resolved = context.Resolve(column.TransformPatterns)?.LastOrDefault()?.ToDataValue();
                if (resolved != null && resolved != DataValue.Undefined())
                {
                    column.DataValue = resolved;
                }
            }
        }
    }
}
