using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Infrastructure;
using TraceForge.Api.Realtime;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddDbContext<TraceForgeDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("TraceForgeDb")));
builder.Services.AddHttpClient<WarehouseFlowClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Erp:BaseUrl"] ?? "http://localhost:5101");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<LumenGridClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["LumenGrid:BaseUrl"] ?? "http://localhost:5103");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ErpOrderSynchronizationService>();
builder.Services.AddScoped<LumenGridExecutionDeliveryService>();
builder.Services.AddHostedService<ErpOrderSyncHostedService>();
builder.Services.AddHostedService<LumenGridExecutionDeliveryHostedService>();
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddPolicy("TraceForgeWeb", policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await TraceForgeDbInitializer.EnsureCreatedAsync(scope.ServiceProvider.GetRequiredService<TraceForgeDbContext>());

// Configure the HTTP request pipeline.

app.UseAuthorization();
app.UseCors("TraceForgeWeb");

app.MapControllers();
app.MapHub<ProductionHub>("/hubs/production");
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options => options.WithTitle("TraceForge MES API"));
}
app.MapGet("/", () => Results.Ok(new
{
    service = "TraceForge MES API",
    status = "running",
    endpoints = new[] { "/api/work-orders", "/api/trace/{batchNo}", "/api/integration/erp/status", "/api/integration/erp/production-orders", "/api/integration/lumengrid/equipment-availability", "/api/integration/lumengrid/equipment-notifications", "/api/integration/equipment-pause-suggestions", "/api/integration/lumengrid/execution-bindings", "/api/integration/lumengrid/execution-deliveries", "/hubs/production" }
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
