using System;

namespace PaymentService.Models
{
    public class PaymentRequest
    {
        public string? OrderId { get; set; }
        public decimal Amount { get; set; }
        public string? CustomerId { get; set; }
    }

    public class PaymentResponse
    {
        public string? OrderId { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
