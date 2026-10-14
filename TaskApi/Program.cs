using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskApi.Endpoints;
using TaskApi.Features.Tasks;
using TaskApi.Infrastructure.RabbitMq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' not found. Set it via user-secrets.");

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqPublisher>());

builder.Services.AddScoped<TaskService>();
builder.Services.AddEndpointGroups();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapEndpoints();

app.Run();

public partial class Program;