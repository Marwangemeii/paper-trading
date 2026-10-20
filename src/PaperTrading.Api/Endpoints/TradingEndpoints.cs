using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaperTrading.Api.Engine;
using PaperTrading.Api.Persistence;
using PaperTrading.Domain;

namespace PaperTrading.Api.Endpoints;

public static class TradingEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public static IEndpointRouteBuilder MapTradingApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/instruments", GetInstruments).WithSummary("Tradable instruments with their last price");
        api.MapPost("/accounts", OpenAccount).WithSummary("Open a paper account with the starting balance");
        api.MapGet("/accounts/{accountId:guid}/portfolio", GetPortfolio).WithSummary("Balance, positions and P&L");
        api.MapPost("/accounts/{accountId:guid}/orders", PlaceOrder)
            .WithSummary("Place a market or limit order")
            .WithDescription($"Requires an `{IdempotencyKeyHeader}` header. Retrying with the same key and body " +
                "returns the original order (200) instead of creating a new one.");
        api.MapGet("/accounts/{accountId:guid}/orders", ListOrders).WithSummary("Order history, newest first");
        api.MapGet("/accounts/{accountId:guid}/orders/{orderId:guid}", GetOrder).WithSummary("One order");
        api.MapDelete("/accounts/{accountId:guid}/orders/{orderId:guid}", CancelOrder).WithSummary("Cancel an open limit order");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<InstrumentDto>>> GetInstruments(EngineHost engine, CancellationToken ct) =>
        TypedResults.Ok(await engine.GetInstrumentsAsync(ct));

    private static async Task<Created<AccountCreatedResponse>> OpenAccount(EngineHost engine, CancellationToken ct)
    {
        var account = await engine.OpenAccountAsync(ct);
        return TypedResults.Created($"/api/accounts/{account.AccountId}/portfolio", account);
    }

    private static async Task<Results<Ok<PortfolioSnapshot>, NotFound>> GetPortfolio(
        Guid accountId, EngineHost engine, CancellationToken ct) =>
        await engine.GetPortfolioAsync(accountId, ct) is { } portfolio
            ? TypedResults.Ok(portfolio)
            : TypedResults.NotFound();

    private static async Task<Results<Created<OrderDto>, Ok<OrderDto>, ProblemHttpResult>> PlaceOrder(
        Guid accountId,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        PlaceOrderRequest request,
        EngineHost engine,
        HttpContext http,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return TypedResults.Problem(
                $"The {IdempotencyKeyHeader} header is required (e.g. a new GUID per order).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var reply = await engine.PlaceOrderAsync(
            new PlaceOrderCommand(
                accountId, idempotencyKey, request.Symbol, request.Side, request.Type, request.Quantity, request.LimitPrice),
            ct);

        switch (reply.Outcome)
        {
            case PlaceOrderOutcome.Created:
                return TypedResults.Created($"/api/accounts/{accountId}/orders/{reply.Order!.Id}", reply.Order);
            case PlaceOrderOutcome.Replayed:
                http.Response.Headers[ReplayedHeader] = "true";
                return TypedResults.Ok(reply.Order!);
            case PlaceOrderOutcome.IdempotencyConflict:
                return TypedResults.Problem(reply.Error, statusCode: StatusCodes.Status409Conflict);
            case PlaceOrderOutcome.AccountNotFound:
                return TypedResults.Problem("Account not found.", statusCode: StatusCodes.Status404NotFound);
            default:
                return TypedResults.Problem(reply.Error, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // History is read straight from the database on the request thread, so it never waits in the
    // engine queue. The worker saves before it answers, so every acknowledged order is visible here.
    private static async Task<Results<Ok<List<OrderDto>>, NotFound>> ListOrders(
        Guid accountId, IDbContextFactory<TradingDbContext> dbFactory, CancellationToken ct, int limit = 100)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId, ct))
        {
            return TypedResults.NotFound();
        }

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.AccountId == accountId)
            .OrderByDescending(o => o.Sequence)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct);

        return TypedResults.Ok(orders.Select(OrderDto.From).ToList());
    }

    private static async Task<Results<Ok<OrderDto>, NotFound>> GetOrder(
        Guid accountId, Guid orderId, IDbContextFactory<TradingDbContext> dbFactory, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.AccountId == accountId, ct);

        return order is null ? TypedResults.NotFound() : TypedResults.Ok(OrderDto.From(order));
    }

    private static async Task<Results<Ok<OrderDto>, NotFound, ProblemHttpResult>> CancelOrder(
        Guid accountId, Guid orderId, EngineHost engine, CancellationToken ct)
    {
        var reply = await engine.CancelOrderAsync(accountId, orderId, ct);
        return reply.Outcome switch
        {
            CancelOrderOutcome.Cancelled => TypedResults.Ok(reply.Order!),
            CancelOrderOutcome.NotOpen => TypedResults.Problem(
                $"Order is already {reply.Order!.Status} and cannot be cancelled.", statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.NotFound(),
        };
    }
}
