using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace UserService.OpenApi;

/// <summary>Adds a JWT "Authorize" button to Swagger UI so protected endpoints can be tried out.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Azure terminates HTTPS in front of the app, so a generated server URL would be http:// and the
        // browser would block it. With no servers listed, Swagger UI calls the API on the page's own origin.
        document.Servers?.Clear();

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token returned by POST /api/auth/login on the User Service."
        };
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
        });
        return Task.CompletedTask;
    }
}
