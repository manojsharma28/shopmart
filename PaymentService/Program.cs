using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Payment endpoint
app.MapPost("/pay", async (ILogger<Program> logger, PaymentRequest request) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.OrderId))
    {
        logger.LogWarning("Invalid payment request");
        return Results.BadRequest(new { error = "Invalid payment request" });
    }

    // Simulate payment processing
    var success = Random.Shared.Next(1, 10) != 7; // 1 in 9 chance of failure
    
    if (success)
    {
        logger.LogInformation("Payment successful for order {OrderId}", request.OrderId);
        return Results.Ok(new PaymentResponse
        {
            OrderId = request.OrderId,
            Success = true,
            Message = "Payment processed successfully"
        });
    }
    else
    {
        logger.LogWarning("Payment failed for order {OrderId}", request.OrderId);
        return Results.Ok(new PaymentResponse
        {
            OrderId = request.OrderId,
            Success = false,
            Message = "Insufficient funds"
        });
    }
});

app.Run();
