using ArchitectureAssistant.Web.Components;
using ArchitectureAssistant.Application;
using ArchitectureAssistant.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var workspaceRoot = builder.Configuration["Workspace:RootPath"];
if (string.IsNullOrWhiteSpace(workspaceRoot))
{
    workspaceRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArchitectureDecisionAssistant",
        "workspace");
}

builder.WebHost.UseUrls("http://127.0.0.1:5182");
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(workspaceRoot);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
