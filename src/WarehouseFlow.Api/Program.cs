using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Application.Planning;
using WarehouseFlow.Api.Application.Partners;
using WarehouseFlow.Api.Application.Documents;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using WarehouseFlow.Api.Realtime;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddScoped<DocumentFlowService>();
builder.Services.AddScoped<PartnerLookupService>();
builder.Services.AddScoped<MrpService>();
builder.Services.AddScoped<DocumentWorkflowService>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT"
            }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuthorization = endpointMetadata.OfType<AuthorizeAttribute>().Any();
        var allowsAnonymous = endpointMetadata.OfType<AllowAnonymousAttribute>().Any();
        if (requiresAuthorization && !allowsAnonymous)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddDbContext<WmsDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("WmsDb")));
builder.Services.AddScoped<DocumentNumberService>();
builder.Services.AddSignalR();
if (string.IsNullOrWhiteSpace(builder.Configuration["Redis:ConnectionString"]))
    builder.Services.AddDistributedMemoryCache();
else
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = builder.Configuration["Redis:ConnectionString"]);

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in PermissionCodes.All)
        options.AddPolicy(permission, policy => policy.RequireClaim(SecurityClaims.Permission, permission));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await WmsDbInitializer.SeedAsync(scope.ServiceProvider.GetRequiredService<WmsDbContext>(), builder.Configuration.GetValue<bool>("SeedDemoData"));

// Configure the HTTP request pipeline.

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<InventoryHub>("/hubs/inventory");
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options => options.WithTitle("WarehouseFlow WMS API"));
}
app.MapGet("/", () => Results.Ok(new
{
    service = "WarehouseFlow WMS API",
    status = "running",
    endpoints = new[]
    {
        "/api/dashboard/summary",
        "/api/master-data/products",
        "/api/inbound",
        "/api/outbound",
        "/api/inventory",
        "/api/auth/token",
        "/hubs/inventory"
    }
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
