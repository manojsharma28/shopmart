using System.Net.Http.Json;
using ShopMart.Models;
using System.Text.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["InventoryService:Url"] ?? "http://inventory-service:8080/");
});

builder.Services.AddHttpClient("order", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OrderService:Url"] ?? "http://order-service:8080/");
});

builder.Services.AddHttpClient("notification", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["NotificationService:Url"] ?? "http://notification-service:8080/");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/inventory", async (IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("inventory");
    var products = await client.GetFromJsonAsync<List<Product>>("api/inventory/products");
    return Results.Ok(products ?? new List<Product>());
});

app.MapPost("/api/order", async (IHttpClientFactory httpClientFactory, OrderCreateRequest orderRequest, ILogger<Program> logger) =>
{
    if (orderRequest == null || string.IsNullOrWhiteSpace(orderRequest.CustomerId) || orderRequest.Items == null || !orderRequest.Items.Any())
    {
        return Results.BadRequest(new { message = "Order requires a customer and one or more items." });
    }

    var client = httpClientFactory.CreateClient("order");
    var request = new OrderRequest
    {
        Id = string.IsNullOrWhiteSpace(orderRequest.Id) ? Guid.NewGuid().ToString() : orderRequest.Id,
        CustomerId = orderRequest.CustomerId,
        Amount = orderRequest.Amount,
        Items = orderRequest.Items
    };

    logger.LogInformation("Forwarding order to OrderService: {@OrderRequest}", request);
    var payload = JsonSerializer.Serialize(request);
    logger.LogInformation("Order payload: {Payload}", payload);
    var response = await client.PostAsync("api/orders", new StringContent(payload, Encoding.UTF8, "application/json"));
    var responseBody = await response.Content.ReadAsStringAsync();
    logger.LogInformation("OrderService responded {StatusCode}: {Body}", response.StatusCode, responseBody);

    if (response.IsSuccessStatusCode)
    {
        var content = await response.Content.ReadFromJsonAsync<object>();
        return Results.Ok(content);
    }

    return Results.Json(string.IsNullOrWhiteSpace(responseBody) ? new { message = "Unknown error from Order Service" } : System.Text.Json.JsonSerializer.Deserialize<object>(responseBody), statusCode: (int)response.StatusCode);
});

app.MapGet("/api/notifications", async (IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("notification");
    var notifications = await client.GetFromJsonAsync<List<NotificationMessage>>("api/notifications");
    return Results.Ok(notifications ?? new List<NotificationMessage>());
});

app.Run();
