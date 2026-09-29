public class DocumentTransformPatternRuntimeTests
{
    [Fact]
    public void TransformPattern_ConvertRuleToInteger_SetContextToTableField_RunsSuccessfully()
    {
        var tableType = JsonSerializer.Deserialize<DataType>(
            """
            {
              "name": "table",
              "columns": {
                "0": { "name": "ID", "type": { "name": "integer", "format": "" } },
                "1": { "name": "Name", "type": { "name": "text", "format": "" } },
                "2": { "name": "Description", "type": { "name": "text", "format": "" } }
              }
            }
            """,
            PatternSerializer.Options)!;

        var tableField = new BatchTypeDocumentField
        {
            Name = "Table",
            Status = DocumentFieldStatus.Active,
            DocumentField = new DocumentField
            {
                Name = "Table",
                Type = tableType
            }
        };

        var document = new Document
        {
            Id = Guid.NewGuid(),
            BatchTypeDocument = new BatchTypeDocument
            {
                DocumentFields = new List<BatchTypeDocumentField> { tableField },
                BusinessRules = new List<DocumentTypeBusinessRule>
                {
                    new()
                    {
                        Name = "Document_Business_Rule_Calculate_200",
                        Status = BusinessRuleStatus.Active,
                                                Pattern = JsonSerializer.Deserialize<IPattern>(
                                                        """
                                                        {
                                                            "kind": "$eval",
                                                            "expression": {
                                                                "operator": "add",
                                                                "expressions": [
                                                                    { "operator": "add", "value": { "type": "integer", "value": 100 } },
                                                                    { "operator": "add", "value": { "type": "integer", "value": 100 } }
                                                                ]
                                                            }
                                                        }
                                                        """,
                                                        PatternSerializer.Options)!
                    }
                }
            },
            FieldValues = new List<DocumentFieldValue>
            {
                new(tableField)
                {
                    Document = null,
                    DocumentField = tableField,
                    DocumentId = Guid.NewGuid(),
                    Value = DataValue.Undefined()
                }
            }
        };

        var pattern = Data.Patterns.Builders.Convert.ToType(
            Objects.Document("rules['Document_Business_Rule_Calculate_200']"),
            new IntegerType());

        EvaluationHelper.AddTransformation(
            pattern,
            true,
            Trans.SetContextTo(Objects.Document("fields['Table'].value")));

        using var container = BuildPatternContainer();
        var resolver = container.Resolve<IPatternResolver>();
        using IPatternContext context = resolver.CreateContext(document, container);
        var result = context.Resolve(pattern);

        var updated = document.FieldValues.Single().Value;

        Assert.NotNull(result);
        Assert.NotNull(updated);
        Assert.Equal("200", updated.Format(new IntegerType()));
    }

    [Fact]
    public void TransformPattern_SetContext_TableValue_WithTypedColumns_RunsSuccessfully()
    {
        var tableType = JsonSerializer.Deserialize<DataType>(
            """
            {
              "name": "table",
              "columns": {
                "0": { "name": "ID", "type": { "name": "integer", "format": "" } },
                "1": { "name": "Name", "type": { "name": "text", "format": "" } },
                "2": { "name": "Description", "type": { "name": "text", "format": "" } }
              }
            }
            """,
            PatternSerializer.Options)!;

        var tableField = new BatchTypeDocumentField
        {
            Name = "Table",
            Status = DocumentFieldStatus.Active,
            DocumentField = new DocumentField
            {
                Name = "Table",
                Type = tableType
            }
        };

        var document = new Document
        {
            Id = Guid.NewGuid(),
            BatchTypeDocument = new BatchTypeDocument
            {
                DocumentFields = new List<BatchTypeDocumentField> { tableField },
                BusinessRules = new List<DocumentTypeBusinessRule>()
            },
            FieldValues = new List<DocumentFieldValue>
            {
                new(tableField)
                {
                    Document = null,
                    DocumentField = tableField,
                    DocumentId = Guid.NewGuid(),
                    Value = DataValue.Undefined()
                }
            }
        };

        var runtimeTableValue = JsonSerializer.Deserialize<DataValue>(
            """
            {
              "type": "table",
              "value": [
                {
                  "0": { "type": "integer", "value": 200 },
                  "1": { "type": "text", "value": "Alice" },
                  "2": { "type": "text", "value": "Sample row" }
                }
              ]
            }
            """,
            PatternSerializer.Options)!;

        var pattern = Objects.Inject(
            Params.Get("table_runtime_value"),
            Trans.SetContextTo(Objects.Document("fields['Table'].value")));

        using var container = BuildPatternContainer();
        var resolver = container.Resolve<IPatternResolver>();
        using IPatternContext context = resolver.CreateContext(document, container);
        context.SetParameter("table_runtime_value", runtimeTableValue);

        var result = context.Resolve(pattern);
        var updated = document.FieldValues.Single().Value;

        Assert.NotNull(result);
        Assert.NotNull(updated);
        Assert.True(updated.IsTable());

        var rows = updated.AsTable().AsEnumerable().ToList();
        Assert.Single(rows);
        Assert.Equal("200", rows[0]["0"].Format(new IntegerType()));
        Assert.Equal("Alice", rows[0]["1"].Format(new TextType()));
        Assert.Equal("Sample row", rows[0]["2"].Format(new TextType()));
    }

    private static IContainer BuildPatternContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(NullLogger.Instance).As<ILogger>();
        builder.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance).SingleInstance();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));

        var dataAssembly = typeof(Data.EF.DbContext).Assembly;
        var resolverType = dataAssembly.GetType("Data.Patterns.PatternResolver", throwOnError: true)!;
        builder.RegisterType(resolverType).As<IPatternResolver>().SingleInstance();

        return builder.Build();
    }