.Include(b => b.DocumentTypes).ThenInclude(d => d.DocumentFields.Where(f => f.Status == DocumentFieldStatus.Active)).ThenInclude(f => f.DocumentField)
            .Include(b => b.DocumentTypes).ThenInclude(d => d.DocumentFields.Where(f => f.Status == DocumentFieldStatus.Active)).ThenInclude(f => f.TableColumns.Where(c => c.Status == DocumentFieldStatus.Active))
			
			
			
			
			CreateMap<BatchTypeDocumentField, DocumentFieldDto>()
                .ForMember(dest => dest.Attributes, opts =>
                {
                    opts.PreCondition(src => src.DocumentLayoutField != null);
                    opts.MapFrom(src => src.DocumentLayoutField.Attributes);
                })
                .ForMember(dest => dest.TableColumns, opts =>
                {
                    opts.PreCondition(src => src.TableColumns?.Count > 0);
                    opts.MapFrom(src => src.TableColumns
                        .OrderBy(c => c.ColumnIndex)
                        .ToDictionary(
                            c => c.ColumnIndex.ToString(),
                            c => new DocumentFieldColumnRuleDto
                            {
                                Name = c.ColumnName,
                                Type = c.ColumnDataType,
                                DataValue = c.DataValue,
                                Requirement = c.Requirement,
                                MinLength = c.MinLength,
                                MaxLength = c.MaxLength,
                                Expressions = c.Expressions,
                                TransformPatterns = c.TransformPatterns,
                                ValidationConditions = c.ValidationConditions
                            }));
                })
                .ForMember(dest => dest.Type, opts => opts.MapFrom(src => src.DocumentField.Type))
                .ForMember(dest => dest.DataSource, opts => opts.MapFrom(src => src.DataSource ?? new ManualDataSource()))
                .BeforeMap((src, _) =>
                {
                    if (string.IsNullOrEmpty(src.Name) && src.DocumentField != null)
                    {
                        src.Name = src.DocumentField.Name;
                        src.IsCustomized = false;
                    }
                });
				
				
				
				
				
				
				
				private static ICollection<BatchTypeDocumentFieldColumn> CreateTableColumns(DocumentFieldDto documentFieldDto, Types.TableType tableType = null)
        {
            tableType ??= documentFieldDto?.Type as Types.TableType;
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
                if (item.Value == null)
                {
                    continue;
                }

                var (resolved, columnIndex, columnType) = ResolveColumn(tableType, item.Key);
                if (!resolved)
                {
                    continue;
                }

                columns.Add(new BatchTypeDocumentFieldColumn
                {
                    ColumnIndex = columnIndex,
                    ColumnName = item.Value.Name ?? columnType.Name,
                    ColumnDataType = item.Value.Type ?? columnType.Type ?? DataType.Undefined,
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
		
		
		
		
		
		
		
		
		private static (bool Resolved, int ColumnIndex,Types.ColumnType ColumnType) ResolveColumn(
           Types.TableType tableType,
            string key)
        {
            if (tableType == null || string.IsNullOrWhiteSpace(key))
            {
                return (false, default, null);
            }

            if (int.TryParse(key, out var columnIndex))
            {
                var indexedColumn = tableType.GetColumns().ElementAtOrDefault(columnIndex);
                return indexedColumn != null ? (true, columnIndex, indexedColumn) : (false, default, null);
            }

            var namedColumns = tableType.GetColumns().Select((column, index) => new { column, index });
            var namedMatch = namedColumns.FirstOrDefault(x => string.Equals(x.column.Name, key, StringComparison.OrdinalIgnoreCase));
            return namedMatch != null ? (true, namedMatch.index, namedMatch.column) : (false, default, null);
        }