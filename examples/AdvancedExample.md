# Advanced FlowSharp Examples

This document demonstrates advanced features of FlowSharp including conditional middleware, branching, exception handling, and cross-cutting concerns.

## Example 1: Conditional Middleware

```csharp
using FlowSharp;

var builder = new PipelineBuilder<int, string>();

// Build-time conditional: Only add logging in debug mode
builder.UseWhen(
    () => Environment.GetEnvironmentVariable("DEBUG") == "true",
    (context, next, ct) =>
    {
        Console.WriteLine($"[DEBUG] Processing: {context}");
        return next(context, ct);
    }
);

// Runtime conditional: Different processing based on input value
builder.UseWhenRuntime(
    context => context > 100,
    (context, next, ct) =>
    {
        Console.WriteLine($"[HIGH VALUE] {context}");
        return Task.FromResult($"Large: {context}");
    }
);

// Default handler
builder.Use((context, next, ct) => Task.FromResult($"Normal: {context}"));

var pipeline = builder.Build();

// Test with different values
var result1 = await pipeline(50, CancellationToken.None);   // "Normal: 50"
var result2 = await pipeline(150, CancellationToken.None);  // "Large: 150"
```

## Example 2: Pipeline Branching

```csharp
using FlowSharp;

public class RequestContext
{
    public string UserRole { get; set; }
    public string Action { get; set; }
    public bool IsAuthenticated { get; set; }
}

var builder = new PipelineBuilder<RequestContext, string>();

// Admin branch
builder.MapWhen(
    context => context.UserRole == "Admin",
    adminBranch =>
    {
        adminBranch.Use((ctx, next, ct) =>
        {
            Console.WriteLine("Admin permissions granted");
            ctx.Action = "AdminAction";
            return next(ctx, ct);
        });
    }
);

// User branch
builder.MapWhen(
    context => context.UserRole == "User",
    userBranch =>
    {
        userBranch.Use((ctx, next, ct) =>
        {
            Console.WriteLine("User permissions granted");
            ctx.Action = "UserAction";
            return next(ctx, ct);
        });
    }
);

// Final handler
builder.Use((context, next, ct) =>
    Task.FromResult($"Executed: {context.Action} for {context.UserRole}"));

var pipeline = builder.Build();

var adminContext = new RequestContext { UserRole = "Admin", IsAuthenticated = true };
var result = await pipeline(adminContext, CancellationToken.None);
// Output: "Executed: AdminAction for Admin"
```

## Example 3: Exception Handling

```csharp
using FlowSharp;

var builder = new PipelineBuilder<string, string>();

// Global exception handler
builder.UseExceptionHandler((context, exception, ct) =>
{
    Console.Error.WriteLine($"Error processing '{context}': {exception.Message}");
    
    // Return a fallback result
    return Task.FromResult($"Error handled: {context}");
});

// Middleware that might throw
builder.Use((context, next, ct) =>
{
    if (context == "invalid")
    {
        throw new InvalidOperationException("Invalid input detected");
    }
    return next(context, ct);
});

// Normal processing
builder.Use((context, next, ct) => Task.FromResult($"Processed: {context}"));

var pipeline = builder.Build();

var result1 = await pipeline("valid", CancellationToken.None);
// Output: "Processed: valid"

var result2 = await pipeline("invalid", CancellationToken.None);
// Output: "Error handled: invalid"
```

## Example 4: Cross-Cutting Concerns (Logging & Timing)

```csharp
using FlowSharp;
using System.Diagnostics;

var builder = new PipelineBuilder<string, string>();

// Timing middleware
var stopwatch = new Stopwatch();
builder.UseAroundInvoke(
    before: context =>
    {
        Console.WriteLine($"Starting pipeline for: {context}");
        stopwatch.Restart();
    },
    after: (context, result) =>
    {
        stopwatch.Stop();
        Console.WriteLine($"Completed in {stopwatch.ElapsedMilliseconds}ms: {result}");
    }
);

// Simulate processing
builder.Use(async (context, next, ct) =>
{
    await Task.Delay(100); // Simulate work
    return context.ToUpper();
});

var pipeline = builder.Build();

var result = await pipeline("hello", CancellationToken.None);
// Output:
// Starting pipeline for: hello
// Completed in 100ms: HELLO
```

## Example 5: Complex Pipeline with Multiple Patterns

```csharp
using FlowSharp;

public class OrderContext
{
    public decimal Amount { get; set; }
    public string CustomerType { get; set; }
    public bool IsUrgent { get; set; }
}

var builder = new PipelineBuilder<OrderContext, string>();

// Exception handling (outermost)
builder.UseExceptionHandler((ctx, ex, ct) =>
{
    return Task.FromResult($"Order failed: {ex.Message}");
});

// Logging
builder.UseAroundInvoke(
    before: ctx => Console.WriteLine($"Processing order: {ctx.Amount:C}"),
    after: (ctx, result) => Console.WriteLine($"Order result: {result}")
);

// Validation
builder.Use((ctx, next, ct) =>
{
    if (ctx.Amount <= 0)
        throw new InvalidOperationException("Invalid amount");
    return next(ctx, ct);
});

// Premium customer branch
builder.MapWhen(
    ctx => ctx.CustomerType == "Premium",
    premiumBranch =>
    {
        premiumBranch.Use((ctx, next, ct) =>
        {
            ctx.Amount *= 0.9m; // 10% discount
            Console.WriteLine("Premium discount applied");
            return next(ctx, ct);
        });
    }
);

// Urgent processing
builder.UseWhenRuntime(
    ctx => ctx.IsUrgent,
    (ctx, next, ct) =>
    {
        Console.WriteLine("URGENT - Priority processing");
        return next(ctx, ct);
    }
);

// Final processing
builder.Use((ctx, next, ct) =>
    Task.FromResult($"Order confirmed: {ctx.Amount:C}"));

var pipeline = builder.Build();

var order = new OrderContext
{
    Amount = 100m,
    CustomerType = "Premium",
    IsUrgent = true
};

var result = await pipeline(order, CancellationToken.None);
// Output:
// Processing order: $100.00
// Premium discount applied
// URGENT - Priority processing
// Order result: Order confirmed: $90.00
```

## Best Practices

1. **Order matters**: Place exception handlers early in the pipeline to catch errors from all downstream middleware.

2. **Use conditional middleware wisely**: 
   - Use `UseWhen()` for conditions known at build time (environment variables, configuration)
   - Use `UseWhenRuntime()` for conditions based on the context

3. **Keep middleware focused**: Each middleware should have a single responsibility.

4. **Test branches independently**: When using `MapWhen()`, ensure each branch is properly tested.

5. **Handle cancellation**: Always respect the `CancellationToken` in long-running operations.

6. **Avoid side effects in predicates**: Predicates for `UseWhenRuntime()` and `MapWhen()` should be pure functions.
