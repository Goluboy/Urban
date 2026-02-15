using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Urban.Domain.Common;

namespace Urban.Persistence.Conventions;

public class BaseEntityConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        var baseType = typeof(BaseEntity);
        var entityTypes = modelBuilder.Metadata.GetEntityTypes()
            .Where(t => t.ClrType.IsAssignableTo(baseType));

        foreach (var entityType in entityTypes)
        {
            var idProperty = entityType.FindProperty(nameof(BaseEntity.Id));
            idProperty?.Builder.HasDefaultValueSql("gen_random_uuid()");
        }
    }
}