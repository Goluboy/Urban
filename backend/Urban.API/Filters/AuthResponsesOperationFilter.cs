using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Urban.API.Filters;

public class AuthResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context == default 
            || context.MethodInfo == default 
            || context.MethodInfo.DeclaringType == default) 
            return;

        var authAttributes = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>();

        if (authAttributes.Any() && operation.Responses.All(r => r.Key != "401"))
            operation.Responses.Add("401", new OpenApiResponse { Description = "Unauthorized" });

        if (authAttributes.Any() && operation.Responses.All(r => r.Key != "403"))
            operation.Responses.Add("403", new OpenApiResponse { Description = "Forbidden" });
    }
}