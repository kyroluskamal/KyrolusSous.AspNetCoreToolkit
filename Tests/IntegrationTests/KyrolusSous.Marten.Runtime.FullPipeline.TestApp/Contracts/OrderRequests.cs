using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Contracts;

public sealed record PlaceOrderRequest(
    string CustomerEmail,
    string PaymentMethod,
    List<OrderLine> Lines);
