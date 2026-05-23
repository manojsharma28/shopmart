using InventoryService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Inventory Service API",
        Version = "v1",
        Description = "API for managing product inventory and stock reservations"
    });
});

builder.Services.AddLogging();

// Register InventoryService
builder.Services.AddSingleton<IInventoryService, InventoryServiceImpl>();

// Register Kafka Consumer Service
builder.Services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Start Kafka consumer in background
var kafkaConsumerService = app.Services.GetRequiredService<IKafkaConsumerService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

var cts = new CancellationTokenSource();
_ = Task.Run(async () =>
{
    try
    {
        logger.LogInformation("Starting Kafka consumer service");
        await kafkaConsumerService.StartConsumingAsync(cts.Token);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Kafka consumer service error");
    }
});

// Graceful shutdown
app.Lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Application stopping - cancelling Kafka consumer");
    cts.Cancel();
});

logger.LogInformation("InventoryService started on port {Port}", 8080);

app.Run();
