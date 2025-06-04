using FlowSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 1) Build a generic host so we can get DI in a console app
using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // 2) Register IPipelineBuilder<,> → PipelineBuilder<,> (Scoped by default)
        services.AddFlowSharp();
    })
    .Build();

// 3) Create a scope (since Pipelines are registered as Scoped)
using var scope = host.Services.CreateScope();
var provider = scope.ServiceProvider;

// 4) Resolve IPipelineBuilder<int,int> from DI
var builder = provider.GetRequiredService<IPipelineBuilder<int, int>>();

//
// 5) Add your first middleware:
//    - Logs “Before: …”
//    - Adds 10 to the incoming context
//    - Calls next(...)
//    - Logs “After: …”
//
builder.Use(async (context, next, cancellationToken) =>
{
    Console.WriteLine($"[Middleware #1] Before: {context}");
    var modifiedContext = context + 10;
    var resultFromNext = await next(modifiedContext, cancellationToken);
    Console.WriteLine($"[Middleware #1] After: {resultFromNext}");
    return resultFromNext;
});

//
// 6) Add your second middleware:
//    - Logs “Before: …”
//    - Multiplies the incoming context by 2
//    - Calls next(...)
//    - Logs “After: …”
//
builder.Use(async (context, next, cancellationToken) =>
{
    Console.WriteLine($"[Middleware #2] Before: {context}");
    var modifiedContext = context * 2;
    var resultFromNext = await next(modifiedContext, cancellationToken);
    Console.WriteLine($"[Middleware #2] After: {resultFromNext}");
    return resultFromNext;
});

//
// 7) Register the “root action” as the final middleware.
//    This terminal middleware does NOT call next(…), so it's effectively the pipeline's end.
//
builder.Use((context, _next, cancellationToken) =>
{
    // This code runs after all other middlewares have forwarded to it.
    Console.WriteLine($"[Root Action] Received: {context}");
    return Task.FromResult(context);
});

//
// 8) Build() with no arguments. Internally, Build() will compose
//    all added middlewares (in registration order) into a single PipelineDelegate<int,int>.
//
var pipeline = builder.Build();

//
// 9) Invoke the pipeline with an initial context of 5
//
var finalResult = await pipeline(5, CancellationToken.None);

Console.WriteLine($"[Program] Final result: {finalResult}");