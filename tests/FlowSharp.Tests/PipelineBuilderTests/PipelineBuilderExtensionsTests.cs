using FluentAssertions;

namespace FlowSharp.Tests.PipelineBuilderTests;

public class PipelineBuilderExtensionsTests
{
    [Fact]
    public async Task UseWhen_PredicateTrue_ExecutesMiddleware()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var middlewareExecuted = false;

        // Act
        builder.UseWhen(
            () => true,
            (context, next, ct) =>
            {
                middlewareExecuted = true;
                return next(context + "-Conditional", ct);
            }
        );

        builder.Use((context, next, ct) => Task.FromResult(context));

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        middlewareExecuted.Should().BeTrue();
        result.Should().Be("Start-Conditional");
    }

    [Fact]
    public async Task UseWhen_PredicateFalse_SkipsMiddleware()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var middlewareExecuted = false;

        // Act
        builder.UseWhen(
            () => false,
            (context, next, ct) =>
            {
                middlewareExecuted = true;
                return next(context + "-Conditional", ct);
            }
        );

        builder.Use((context, next, ct) => Task.FromResult(context));

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        middlewareExecuted.Should().BeFalse();
        result.Should().Be("Start");
    }

    [Fact]
    public async Task UseWhenRuntime_PredicateTrue_ExecutesMiddleware()
    {
        // Arrange
        var builder = new PipelineBuilder<int, string>();

        // Act
        builder.UseWhenRuntime(
            context => context > 5,
            (context, next, ct) => Task.FromResult($"High-{context}")
        );

        builder.Use((context, next, ct) => Task.FromResult($"Normal-{context}"));

        var pipeline = builder.Build();
        var result = await pipeline(10, CancellationToken.None);

        // Assert
        result.Should().Be("High-10");
    }

    [Fact]
    public async Task UseWhenRuntime_PredicateFalse_SkipsToNext()
    {
        // Arrange
        var builder = new PipelineBuilder<int, string>();

        // Act
        builder.UseWhenRuntime(
            context => context > 5,
            (context, next, ct) => Task.FromResult($"High-{context}")
        );

        builder.Use((context, next, ct) => Task.FromResult($"Normal-{context}"));

        var pipeline = builder.Build();
        var result = await pipeline(3, CancellationToken.None);

        // Assert
        result.Should().Be("Normal-3");
    }

    [Fact]
    public async Task MapWhen_PredicateTrue_ExecutesBranch()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var branchExecuted = false;

        // Act
        builder.MapWhen(
            context => context.StartsWith("Admin"),
            branch =>
            {
                branch.Use((context, next, ct) =>
                {
                    branchExecuted = true;
                    return next(context + "-AdminPath", ct);
                });
            }
        );

        builder.Use((context, next, ct) => Task.FromResult(context + "-End"));

        var pipeline = builder.Build();
        var result = await pipeline("AdminUser", CancellationToken.None);

        // Assert
        branchExecuted.Should().BeTrue();
        result.Should().Be("AdminUser-AdminPath-End");
    }

    [Fact]
    public async Task MapWhen_PredicateFalse_SkipsBranch()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var branchExecuted = false;

        // Act
        builder.MapWhen(
            context => context.StartsWith("Admin"),
            branch =>
            {
                branch.Use((context, next, ct) =>
                {
                    branchExecuted = true;
                    return next(context + "-AdminPath", ct);
                });
            }
        );

        builder.Use((context, next, ct) => Task.FromResult(context + "-End"));

        var pipeline = builder.Build();
        var result = await pipeline("RegularUser", CancellationToken.None);

        // Assert
        branchExecuted.Should().BeFalse();
        result.Should().Be("RegularUser-End");
    }

    [Fact]
    public async Task UseExceptionHandler_CatchesException_ReturnsHandlerResult()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        builder.UseExceptionHandler((context, ex, ct) =>
        {
            return Task.FromResult($"Error: {ex.Message}");
        });

        builder.Use((context, next, ct) =>
        {
            throw new InvalidOperationException("Test exception");
        });

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        result.Should().Be("Error: Test exception");
    }

    [Fact]
    public async Task UseExceptionHandler_NoException_ReturnsNormalResult()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        builder.UseExceptionHandler((context, ex, ct) =>
        {
            return Task.FromResult($"Error: {ex.Message}");
        });

        builder.Use((context, next, ct) => Task.FromResult(context + "-Success"));

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        result.Should().Be("Start-Success");
    }

    [Fact]
    public async Task UseAroundInvoke_ExecutesBeforeAndAfter()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var beforeExecuted = false;
        var afterExecuted = false;
        string? capturedResult = null;

        // Act
        builder.UseAroundInvoke(
            before: context => beforeExecuted = true,
            after: (context, result) =>
            {
                afterExecuted = true;
                capturedResult = result;
            }
        );

        builder.Use((context, next, ct) => Task.FromResult(context + "-Processed"));

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        beforeExecuted.Should().BeTrue();
        afterExecuted.Should().BeTrue();
        capturedResult.Should().Be("Start-Processed");
        result.Should().Be("Start-Processed");
    }

    [Fact]
    public async Task UseAroundInvoke_OnlyBefore_ExecutesCorrectly()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var beforeExecuted = false;

        // Act
        builder.UseAroundInvoke(before: context => beforeExecuted = true);

        builder.Use((context, next, ct) => Task.FromResult(context + "-Processed"));

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        beforeExecuted.Should().BeTrue();
        result.Should().Be("Start-Processed");
    }

    [Fact]
    public async Task UseAroundInvoke_OnlyAfter_ExecutesCorrectly()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();
        var afterExecuted = false;

        // Act
        builder.UseAroundInvoke(after: (context, result) => afterExecuted = true);

        builder.Use((context, next, ct) => Task.FromResult(context + "-Processed"));

        var pipeline = builder.Build();
        var result = await pipeline("Start", CancellationToken.None);

        // Assert
        afterExecuted.Should().BeTrue();
        result.Should().Be("Start-Processed");
    }

    [Fact]
    public void UseWhen_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        IPipelineBuilder<string, string> nullBuilder = null!;

        // Act
        Action act = () => nullBuilder.UseWhen(() => true, (ctx, next, ct) => Task.FromResult(""));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseWhen_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        Action act = () => builder.UseWhen(null!, (ctx, next, ct) => Task.FromResult(""));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
    }

    [Fact]
    public void UseWhenRuntime_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        Action act = () => builder.UseWhenRuntime(null!, (ctx, next, ct) => Task.FromResult(""));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
    }

    [Fact]
    public void UseWhenRuntime_NullMiddleware_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        Action act = () => builder.UseWhenRuntime(ctx => true, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("middleware");
    }

    [Fact]
    public void MapWhen_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        Action act = () => builder.MapWhen(null!, branch => { });

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
    }

    [Fact]
    public void MapWhen_NullBranchConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        Action act = () => builder.MapWhen(ctx => true, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("branchConfiguration");
    }

    [Fact]
    public void UseExceptionHandler_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        IPipelineBuilder<string, string> nullBuilder = null!;

        // Act
        Action act = () => nullBuilder.UseExceptionHandler((ctx, ex, ct) => Task.FromResult(""));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseExceptionHandler_NullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PipelineBuilder<string, string>();

        // Act
        Action act = () => builder.UseExceptionHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("exceptionHandler");
    }

    [Fact]
    public void UseAroundInvoke_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        IPipelineBuilder<string, string> nullBuilder = null!;

        // Act
        Action act = () => nullBuilder.UseAroundInvoke();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public async Task ComplexPipeline_WithMultipleExtensions_ExecutesCorrectly()
    {
        // Arrange
        var builder = new PipelineBuilder<int, string>();
        var log = new List<string>();

        // Act - Build a complex pipeline with multiple patterns
        builder.UseAroundInvoke(
            before: ctx => log.Add($"Before: {ctx}"),
            after: (ctx, result) => log.Add($"After: {result}")
        );

        builder.UseExceptionHandler((ctx, ex, ct) =>
        {
            log.Add($"Exception: {ex.Message}");
            return Task.FromResult("Error");
        });

        builder.UseWhenRuntime(
            ctx => ctx > 10,
            (ctx, next, ct) =>
            {
                log.Add("High value path");
                return next(ctx, ct);
            }
        );

        builder.MapWhen(
            ctx => ctx % 2 == 0,
            branch =>
            {
                branch.Use((ctx, next, ct) =>
                {
                    log.Add("Even number branch");
                    return next(ctx, ct);
                });
            }
        );

        builder.Use((ctx, next, ct) =>
        {
            log.Add($"Final: {ctx}");
            return Task.FromResult($"Result-{ctx}");
        });

        var pipeline = builder.Build();
        var result = await pipeline(12, CancellationToken.None);

        // Assert
        result.Should().Be("Result-12");
        log.Should().ContainInOrder(
            "Before: 12",
            "High value path",
            "Even number branch",
            "Final: 12",
            "After: Result-12"
        );
    }
}
