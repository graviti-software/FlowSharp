namespace FlowSharp
{
    /// <summary>
    /// Provides a concrete implementation of <see cref="IPipelineBuilder{TContext, TResult}"/>
    /// for building and executing a middleware pipeline.
    /// <para>
    /// Registered middleware (via <c>Use</c>) are stored in the order of registration. When <c>Build</c> is called,
    /// they are composed so that the first‐registered middleware is executed first and the last‐registered one executes
    /// immediately before the terminal delegate (which returns <c>default(TResult)</c>).
    /// </para>
    /// <para>
    /// This class is thread‐safe for concurrent <c>Use</c> calls until <c>Build</c> is invoked. After building, no further
    /// registrations are permitted, and subsequent <c>Use</c> calls cause an <see cref="InvalidOperationException"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the pipeline.</typeparam>
    internal class PipelineBuilder<TContext, TResult> : IPipelineBuilder<TContext, TResult>
    {
        // Each component maps an “inner” delegate to an “outer” delegate.
        private readonly IList<Func<PipelineDelegate<TContext, TResult>, PipelineDelegate<TContext, TResult>>> _components
            = [];

        private readonly object _lockHandle = new();
        private bool _isBuilt = false;

        /// <summary>
        /// Registers a middleware delegate in the pipeline by wrapping it in an <see cref="InlineMiddleware"/>.
        /// </summary>
        /// <param name="middleware">
        /// A delegate of signature:
        /// <c>Func&lt;TContext, PipelineDelegate&lt;TContext, TResult&gt;, CancellationToken, Task&lt;TResult&gt;&gt;</c>.
        /// </param>
        /// <returns>The same builder instance for chaining.</returns>
        public IPipelineBuilder<TContext, TResult> Use(
            Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>> middleware
        )
        {
            ArgumentNullException.ThrowIfNull(middleware);

            return Use(new InlineMiddleware(middleware));
        }

        /// <summary>
        /// Registers an <see cref="IMiddleware{TContext, TResult}"/> implementation in the pipeline.
        /// </summary>
        /// <param name="middleware">The middleware object to register.</param>
        /// <returns>The same builder instance for chaining.</returns>
        public IPipelineBuilder<TContext, TResult> Use(IMiddleware<TContext, TResult> middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            lock (_lockHandle)
            {
                if (_isBuilt)
                    throw new InvalidOperationException("Cannot add middleware after the pipeline has been built.");

                // Wrap the middleware’s InvokeAsync method so it fits PipelineDelegate<TContext, TResult>.
                _components.Add(next => (context, cancellationToken) =>
                    middleware.InvokeAsync(context, next, cancellationToken)
                );

                return this;
            }
        }

        /// <summary>
        /// Builds a <see cref="PipelineDelegate{TContext, TResult}"/> that composes all registered middleware.
        /// <para>
        /// The returned delegate can be invoked multiple times. If <c>Use</c> is called after this, an exception is thrown.
        /// </para>
        /// </summary>
        /// <returns>
        /// A <see cref="PipelineDelegate{TContext, TResult}"/> representing the entire middleware chain.
        /// If no middleware short‐circuits, it returns <c>default(TResult)</c> (which is <c>null</c> for reference types).
        /// </returns>
        public PipelineDelegate<TContext, TResult> Build()
        {
            lock (_lockHandle)
            {
                if (_isBuilt)
                    throw new InvalidOperationException("Pipeline has already been built.");

                // Terminal delegate: returns default(TResult).
                // For reference types, that is null; for value types, it's the zero-equivalent.
                PipelineDelegate<TContext, TResult> app = (context, cancellationToken) =>
                    Task.FromResult(default(TResult)!);

                // Compose in reverse order so that the first‐registered middleware runs first.
                for (int i = _components.Count - 1; i >= 0; i--)
                {
                    app = _components[i](app);
                }

                _isBuilt = true;
                return app;
            }
        }

        /// <summary>
        /// Wraps a delegate‐based middleware function into an <see cref="IMiddleware{TContext, TResult}"/> instance.
        /// </summary>
        private class InlineMiddleware(
            Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>> middleware
            ) : IMiddleware<TContext, TResult>
        {
            private readonly Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>> _middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));

            public Task<TResult> InvokeAsync(
                TContext context,
                PipelineDelegate<TContext, TResult> next,
                CancellationToken cancellationToken
            )
            {
                return _middleware(context, next, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Provides static helper methods for constructing and executing middleware pipelines without using DI.
    /// </summary>
    public static class PipelineBuilder
    {
        /// <summary>
        /// Executes a middleware pipeline with the specified <paramref name="rootAction"/> and any registered <paramref name="middlewares"/>.
        /// <para>
        /// This method builds a new pipeline: it registers each <paramref name="middlewares"/> in order, then appends
        /// <paramref name="rootAction"/> as the final component. The resulting delegate is invoked immediately with <paramref name="context"/>.
        /// </para>
        /// <para>
        /// If no middleware short‐circuits, <paramref name="rootAction"/> produces the final <typeparamref name="TResult"/>.
        /// </para>
        /// </summary>
        /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
        /// <typeparam name="TResult">The type of the result returned by the pipeline.</typeparam>
        /// <param name="rootAction">
        /// A delegate of signature <c>Func&lt;CancellationToken, Task&lt;TResult&gt;&gt;</c> representing the terminal operation.
        /// Note: this delegate does not receive <c>TContext</c>—only the <see cref="CancellationToken"/>. If you need
        /// <c>rootAction</c> to observe <c>TContext</c>, use <see cref="RunAsync{TContext, TResult}(Func&lt;TContext, CancellationToken, Task&lt;TResult&gt;&gt;, TContext, IMiddleware&lt;TContext, TResult&gt;[], CancellationToken)"/>.
        /// </param>
        /// <param name="context">The context instance to pass into the pipeline.</param>
        /// <param name="middlewares">
        /// An optional array of <see cref="IMiddleware{TContext, TResult}"/> implementations to register in order.
        /// </param>
        /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the result of the pipeline execution.</returns>
        public static Task<TResult> RunAsync<TContext, TResult>(
            Func<CancellationToken, Task<TResult>> rootAction,
            TContext context,
            IMiddleware<TContext, TResult>[]? middlewares = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(rootAction);

            var builder = new PipelineBuilder<TContext, TResult>();
            if (middlewares is not null)
            {
                foreach (var middleware in middlewares)
                {
                    if (middleware is null)
                        throw new ArgumentException("Middleware array contains a null element.", nameof(middlewares));
                    builder.Use(middleware);
                }
            }

            // Append rootAction as the last step (it ignores TContext here).
            builder.Use(async (ctx, next, ct) => await rootAction(ct));

            var pipeline = builder.Build();
            return pipeline(context, cancellationToken);
        }

        /// <summary>
        /// Executes a middleware pipeline with a root action that also observes the <paramref name="context"/>.
        /// <para>
        /// This overload allows <paramref name="rootAction"/> to receive both <paramref name="context"/> and <paramref name="cancellationToken"/>.
        /// It builds a new pipeline, registers <paramref name="middlewares"/> in order, then appends <paramref name="rootAction"/> at the end.
        /// </para>
        /// </summary>
        /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
        /// <typeparam name="TResult">The type of the result returned by the pipeline.</typeparam>
        /// <param name="rootAction">
        /// A delegate of signature <c>Func&lt;TContext, CancellationToken, Task&lt;TResult&gt;&gt;</c> representing the terminal operation.
        /// </param>
        /// <param name="context">The context instance to pass into the pipeline.</param>
        /// <param name="middlewares">
        /// An optional array of <see cref="IMiddleware{TContext, TResult}"/> implementations to register in order.
        /// </param>
        /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the result of the pipeline execution.</returns>
        public static Task<TResult> RunAsync<TContext, TResult>(
            Func<TContext, CancellationToken, Task<TResult>> rootAction,
            TContext context,
            IMiddleware<TContext, TResult>[]? middlewares = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(rootAction);

            var builder = new PipelineBuilder<TContext, TResult>();
            if (middlewares is not null)
            {
                foreach (var middleware in middlewares)
                {
                    if (middleware is null)
                        throw new ArgumentException("Middleware array contains a null element.", nameof(middlewares));
                    builder.Use(middleware);
                }
            }

            // Wrap rootAction so it fits the IMiddleware signature
            builder.Use(async (ctx, next, ct) => await rootAction(ctx, ct));

            var pipeline = builder.Build();
            return pipeline(context, cancellationToken);
        }
    }
}