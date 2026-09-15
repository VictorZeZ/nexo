using nexo.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddNexoProblemDetails();
builder.Services.AddNexoCors(builder.Configuration);
builder.Services.AddNexoRateLimiting(builder.Configuration);
builder.Services.AddNexoRedis(builder.Configuration);
builder.Services.AddNexoWebSocketProtocol(builder.Configuration);
builder.Services.AddNexoWebSocketConnections(builder.Configuration);
builder.Services.AddNexoRooms(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseNexoRequestPipeline();

app.MapControllers();
app.MapNexoWebSocketEndpoint();

app.Run();