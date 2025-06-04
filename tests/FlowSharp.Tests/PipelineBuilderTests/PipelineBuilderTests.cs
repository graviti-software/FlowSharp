using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;  // for ServiceCollection & ServiceLifetime

namespace FlowSharp.Tests.PipelineBuilderTests;

public class PipelineBuilderTests
{
    [Fact]
    public void CanResolve_PipelineBuilder_From_ServiceProvider()
    {
        // 1. Create a new ServiceCollection (fresh DI container)
        var services = new ServiceCollection();

        // 2. Call the non‐generic extension method AddPipelineBuilder,
        //    passing ServiceLifetime.Transient (or another lifetime if you prefer).
        //    This registers the open‐generic IPipelineBuilder<,> → PipelineBuilder<,>.
        services.AddFlowSharp(ServiceLifetime.Transient);

        // 3. Build the ServiceProvider
        var provider = services.BuildServiceProvider();

        // 4. Now ask for the CLOSED‐generic IPipelineBuilder<string, string>:
        var builder = provider.GetService<IPipelineBuilder<string, string>>();

        // 5. Assert that it was registered and is not null:
        builder.Should().NotBeNull(
            "because AddPipelineBuilder must register IPipelineBuilder<,> so that IPipelineBuilder<string,string> can be resolved"
        );
    }

    [Fact]
    public async Task PipelineExecutes_Middleware_In_Order()
    {
        // 1. Create ServiceCollection & register the pipeline (open-generic)
        var services = new ServiceCollection();
        services.AddFlowSharp(ServiceLifetime.Transient);

        // 2. Build ServiceProvider and resolve a builder for <string, string>
        var provider = services.BuildServiceProvider();
        var builder = provider.GetService<IPipelineBuilder<string, string>>();
        builder.Should().NotBeNull();

        // 3. Add two middleware components in sequence:
        //
        //    a) First middleware appends "-A" to the incoming string,
        //       then calls the next delegate in the chain.
        //
        //    b) Second middleware appends "-B" to the string and then RETURNS
        //       the final result (i.e. does NOT call next, so ends the pipeline).
        //
        //    The signature for Use(...) is:
        //      Use(Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>>)
        //
        //    In other words: (context, next, cancellationToken) => Task<TResult>.
        builder.Use((context, next, ct) =>
        {
            // Append "-A" and continue to the next middleware:
            var newContext = context + "-A";
            return next(newContext, ct);
        });

        builder.Use((context, next, ct) =>
        {
            // Append "-B" and return immediately (no further next invocation):
            var finalResult = context + "-B";
            return Task.FromResult(finalResult);
        });

        // 4. Build the final pipeline delegate
        //    The Build() method returns a PipelineDelegate<string, string>, whose signature is:
        //      Task<string> PipelineDelegate(string context, CancellationToken ct)
        var pipeline = builder.Build();

        // 5. Execute the pipeline with an initial input string:
        var input = "Hello";
        var output = await pipeline(input, CancellationToken.None);

        // 6. Assert that both middleware ran in order:
        //
        //    Expected flow:
        //      Start: "Hello"
        //      After first middleware: "Hello-A"
        //      After second middleware: "Hello-A-B"
        output.Should().Be("Hello-A-B");
    }
}

/// <summary>
/// A simple IMiddleware implementation that appends a fixed string to the context,
/// then either calls next(...) if provided, or returns immediately.
/// </summary>
internal class AppendMiddleware(string suffix) : IMiddleware<string, string>
{
    private readonly string _suffix = suffix;

    public Task<string> InvokeAsync(string context, PipelineDelegate<string, string> next, CancellationToken cancellationToken)
    {
        // Append our suffix
        var newCtx = context + _suffix;

        // If “next” is non-null, forward the new context to it; otherwise return immediately.
        return next is not null
            ? next(newCtx, cancellationToken)
            : Task.FromResult(newCtx);
    }
}

public class PipelineBuilderStaticHelperTests
{
    [Fact]
    public void RunAsync_Overload1_ThrowsOnNullRootAction()
    {
        // Overload#1: Func<CancellationToken, Task<TResult>> rootAction
        Func<CancellationToken, Task<string>> rootAction = null!;

        // Because the static helper does ArgumentNullException.ThrowIfNull(rootAction) at the very top,
        // calling RunAsync(…) with a null rootAction should throw synchronously.
        Action act = () => _ = PipelineBuilder.RunAsync(rootAction, "ignored");

        act.Should()
           .Throw<ArgumentNullException>()
           .WithParameterName("rootAction");
    }

    [Fact]
    public async Task RunAsync_Overload1_InvokesRootAction_IgnoringContext()
    {
        // Overload#1: rootAction only takes a CancellationToken—ignores TContext.
        // Even if we pass middlewares that mutate context, rootAction's return value is entirely independent.
        // In this test, rootAction always returns "FINAL".

        // Root action that ignores context and returns “FINAL”
        static Task<string> rootAction(CancellationToken ct) => Task.FromResult("FINAL");

        // Middleware array that would “mutate” context if rootAction observed it.
        // But since rootAction doesn’t receive TContext, the final result remains "FINAL".
        var middlewares = new IMiddleware<string, string>[]
        {
                new AppendMiddleware("-A"),
                new AppendMiddleware("-B")
        };

        // Run the pipeline:
        //   - Context is "Hello" (but rootAction does not see it)
        //   - Middlewares run in order, but rootAction ignores context and returns "FINAL" anyway
        var result = await PipelineBuilder.RunAsync(rootAction, "Hello", middlewares);

        // We expect "FINAL", not "Hello-A-B-…"
        result.Should().Be("FINAL");
    }

    [Fact]
    public void RunAsync_Overload2_ThrowsOnNullRootAction()
    {
        // Overload#2: Func<TContext, CancellationToken, Task<TResult>> rootAction
        // Passing null for rootAction should throw immediately.
        Func<string, CancellationToken, Task<string>> rootAction = null!;

        Action act = () => _ = PipelineBuilder.RunAsync(rootAction, "ignored");

        act.Should()
           .Throw<ArgumentNullException>()
           .WithParameterName("rootAction");
    }

    [Fact]
    public async Task RunAsync_Overload2_ExecutesMiddlewares_ThenRootActionSeesModifiedContext()
    {
        // Overload#2: rootAction sees both context and CancellationToken.
        //
        // We supply two AppendMiddleware:
        //   1. Appends “-A”
        //   2. Appends “-B”
        // Then the rootAction should receive “Hello-A-B” as its context parameter.
        // Finally, rootAction returns context + “-ROOT”.

        var middlewares = new IMiddleware<string, string>[]
        {
                new AppendMiddleware("-A"),
                new AppendMiddleware("-B")
        };

        // Root action will see the mutated context and append “-ROOT”.
        static Task<string> rootAction(string ctx, CancellationToken ct) =>
            Task.FromResult(ctx + "-ROOT");

        var result = await PipelineBuilder.RunAsync(rootAction, "Hello", middlewares);

        // Expected: "Hello-A-B-ROOT"
        result.Should().Be("Hello-A-B-ROOT");
    }

    [Fact]
    public async Task RunAsync_Overload2_NoMiddlewares_CallsRootDirectly()
    {
        // When no middlewares are supplied, the pipeline should immediately invoke rootAction
        // with the original context ("Start") and return its result.

        static Task<string> rootAction(string ctx, CancellationToken ct) =>
            Task.FromResult("Got:" + ctx);

        // middlewares array is null => no middleware to register
        var result = await PipelineBuilder.RunAsync(rootAction, "Start", null);

        // Expect "Got:Start"
        result.Should().Be("Got:Start");
    }

    [Fact]
    public void RunAsync_Overload2_MiddlewaresArrayContainsNull_ThrowsArgumentException()
    {
        // If the array of IMiddleware contains a null element, the helper must throw ArgumentException.
        // It happens when the helper enumerates "middlewares" and finds a null.

        IMiddleware<string, string>[] middlewaresWithNull =
        [
                new AppendMiddleware("-X"),
                null!,                       // invalid
                new AppendMiddleware("-Y"),
        ];

        static Task<string> rootAction(string ctx, CancellationToken ct) =>
            Task.FromResult(ctx);

        Action act = () =>
        {
            // Because the static helper has:
            //   if (middlewares is not null)
            //   {
            //       foreach (var middleware in middlewares)
            //       {
            //           if (middleware is null)
            //             throw new ArgumentException("Middleware array contains a null element.", nameof(middlewares));
            //           builder.Use(middleware);
            //       }
            //   }
            _ = PipelineBuilder.RunAsync(rootAction, "whatever", middlewaresWithNull).Result;
        };

        act.Should()
           .Throw<ArgumentException>()
           .WithMessage("Middleware array contains a null element.*")
           .WithParameterName("middlewares");
    }
}