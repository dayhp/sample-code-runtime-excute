public static void ConfigureBatchTypeDocumentFieldColumn(this EntityTypeBuilder<BatchTypeDocumentFieldColumn> builder)
        {
            builder.HasKey(p => new { p.BatchTypeId, p.DocumentTypeId, p.DocumentFieldId, p.ColumnIndex });
            builder.Property(p => p.ColumnName).HasDefaultLength();
            builder.Property(p => p.Requirement).HasTinyLength().UsePropertyNameConverter();
            builder.Property(p => p.Status).UsePropertyIdConverter(Status.Active);
            builder.HasTracking();

            builder.HasOne(p => p.DocumentField)
                .WithMany(p => p.TableColumns)
                .HasForeignKey(p => new { p.BatchTypeId, p.DocumentTypeId, p.DocumentFieldId })
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(p => p.Status < DocumentFieldStatus.Deleted);
            builder.ToTable(TableNames.BatchTypeDocumentFieldColumns);
        }