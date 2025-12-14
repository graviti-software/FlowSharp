namespace FlowSharp;

/// <summary>
/// Provides extension methods for <see cref="IPipelineBuilder{TContext, TResult}"/> to enable
/// conditional execution, branching, and other common pipeline patterns.
/// </summary>
public static class PipelineBuilderExtensions
{
    /// <summary>
    /// Conditionally registers middleware in the pipeline based on a predicate.
    /// <para>
    /// If the <paramref name="predicate"/> evaluates to <c>true</c>, the specified <paramref name="middleware"/>
    /// is registered; otherwise, it is skipped. This is useful for enabling middleware only in specific
    /// environments or configurations.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    /// <param name="builder">The pipeline builder instance.</param>
    /// <param name="predicate">A function that determines whether to register the middleware.</param>
    /// <param name="middleware">The middleware delegate to conditionally register.</param>
    /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="predicate"/> is null.</exception>
    public static IPipelineBuilder<TContext, TResult> UseWhen<TContext, TResult>(
        this IPipelineBuilder<TContext, TResult> builder,
        Func<bool> predicate,
        Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>> middleware
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);

        if (predicate())
        {
            builder.Use(middleware);
        }

        return builder;
    }

    /// <summary>
    /// Conditionally registers middleware in the pipeline based on a predicate.
    /// <para>
    /// If the <paramref name="predicate"/> evaluates to <c>true</c>, the specified <paramref name="middleware"/>
    /// is registered; otherwise, it is skipped.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    /// <param name="builder">The pipeline builder instance.</param>
    /// <param name="predicate">A function that determines whether to register the middleware.</param>
    /// <param name="middleware">The middleware component to conditionally register.</param>
    /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="predicate"/> is null.</exception>
    public static IPipelineBuilder<TContext, TResult> UseWhen<TContext, TResult>(
        this IPipelineBuilder<TContext, TResult> builder,
        Func<bool> predicate,
        IMiddleware<TContext, TResult> middleware
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);

        if (predicate())
        {
            builder.Use(middleware);
        }

        return builder;
    }

    /// <summary>
    /// Registers middleware that executes conditionally based on runtime context evaluation.
    /// <para>
    /// Unlike <see cref="UseWhen{TContext, TResult}(IPipelineBuilder{TContext, TResult}, Func{bool}, Func{TContext, PipelineDelegate{TContext, TResult}, CancellationToken, Task{TResult}})"/>,
    /// which evaluates the predicate at pipeline build time, this method evaluates the predicate
    /// at runtime for each invocation. If the predicate returns <c>true</c>, the middleware executes;
    /// otherwise, the pipeline proceeds to the next component.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    /// <param name="builder">The pipeline builder instance.</param>
    /// <param name="predicate">A function that receives the context and determines whether to execute the middleware.</param>
    /// <param name="middleware">The middleware delegate to conditionally execute.</param>
    /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="predicate"/>, or <paramref name="middleware"/> is null.</exception>
    public static IPipelineBuilder<TContext, TResult> UseWhenRuntime<TContext, TResult>(
        this IPipelineBuilder<TContext, TResult> builder,
        Func<TContext, bool> predicate,
        Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>> middleware
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(middleware);

        return builder.Use(async (context, next, cancellationToken) =>
        {
            if (predicate(context))
            {
                return await middleware(context, next, cancellationToken);
            }

            return await next(context, cancellationToken);
        });
    }

    /// <summary>
    /// Branches the pipeline execution based on a runtime predicate.
    /// <para>
    /// If the <paramref name="predicate"/> evaluates to <c>true</c>, the pipeline executes the branch
    /// configured by <paramref name="branchConfiguration"/> before proceeding to the next middleware.
    /// Otherwise, execution continues directly to the next middleware.
    /// </para>
    /// <para>
    /// This is useful for implementing conditional workflows where different processing paths
    /// are taken based on context state.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    /// <param name="builder">The pipeline builder instance.</param>
    /// <param name="predicate">A function that receives the context and determines whether to execute the branch.</param>
    /// <param name="branchConfiguration">
    /// An action that configures the branch pipeline. The branch receives its own isolated builder
    /// that shares the same context and result types.
    /// </param>
    /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static IPipelineBuilder<TContext, TResult> MapWhen<TContext, TResult>(
        this IPipelineBuilder<TContext, TResult> builder,
        Func<TContext, bool> predicate,
        Action<IPipelineBuilder<TContext, TResult>> branchConfiguration
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(branchConfiguration);

        return builder.Use(async (context, next, cancellationToken) =>
        {
            if (predicate(context))
            {
                // Create and configure the branch pipeline
                var branchBuilder = new PipelineBuilder<TContext, TResult>();
                branchConfiguration(branchBuilder);

                // Add the continuation (next) as the final step in the branch
                branchBuilder.Use((ctx, _, ct) => next(ctx, ct));

                // Build and execute the branch
                var branchPipeline = branchBuilder.Build();
                return await branchPipeline(context, cancellationToken);
            }

            return await next(context, cancellationToken);
        });
    }

    /// <summary>
    /// Wraps the pipeline with exception handling middleware.
    /// <para>
    /// If any middleware in the pipeline throws an exception, the <paramref name="exceptionHandler"/>
    /// is invoked with the context, exception, and cancellation token. The handler can return a result
    /// to recover from the error or rethrow the exception.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    /// <param name="builder">The pipeline builder instance.</param>
    /// <param name="exceptionHandler">
    /// A function that handles exceptions thrown during pipeline execution.
    /// It receives the context, exception, and cancellation token, and can return a result or rethrow.
    /// </param>
    /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="exceptionHandler"/> is null.</exception>
    public static IPipelineBuilder<TContext, TResult> UseExceptionHandler<TContext, TResult>(
        this IPipelineBuilder<TContext, TResult> builder,
        Func<TContext, Exception, CancellationToken, Task<TResult>> exceptionHandler
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(exceptionHandler);

        return builder.Use(async (context, next, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken);
            }
            catch (Exception ex)
            {
                return await exceptionHandler(context, ex, cancellationToken);
            }
        });
    }

    /// <summary>
    /// Registers middleware that executes an action before and after the next middleware.
    /// <para>
    /// This is useful for implementing cross-cutting concerns like logging, timing, or metrics collection.
    /// The <paramref name="before"/> action is executed before calling the next middleware,
    /// and the <paramref name="after"/> action is executed after receiving the result.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    /// <param name="builder">The pipeline builder instance.</param>
    /// <param name="before">An action to execute before the next middleware. Receives the context.</param>
    /// <param name="after">An action to execute after the next middleware. Receives the context and result.</param>
    /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static IPipelineBuilder<TContext, TResult> UseAroundInvoke<TContext, TResult>(
        this IPipelineBuilder<TContext, TResult> builder,
        Action<TContext>? before = null,
        Action<TContext, TResult>? after = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use(async (context, next, cancellationToken) =>
        {
            before?.Invoke(context);
            var result = await next(context, cancellationToken);
            after?.Invoke(context, result);
            return result;
        });
    }
}
