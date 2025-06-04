namespace FlowSharp
{
    /// <summary>
    /// Defines a builder interface for constructing a middleware pipeline that processes a context and produces a result asynchronously.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">
    /// The type of the result produced by the pipeline execution. If the pipeline has no middleware (or none short‐circuit),
    /// the terminal delegate returns <c>default(TResult)</c> (which is <c>null</c> for reference types, or the zero‐equivalent for value types).
    /// </typeparam>
    public interface IPipelineBuilder<TContext, TResult>
    {
        /// <summary>
        /// Registers a middleware delegate in the pipeline.
        /// <para>
        /// The <paramref name="middleware"/> delegate receives the current context, a delegate to invoke the next middleware,
        /// and a <see cref="CancellationToken"/>. It should return a <see cref="Task{TResult}"/> containing the result
        /// (either by awaiting <paramref name="next"/> or by short‐circuiting with its own return value).
        /// </para>
        /// <para>
        /// Middleware is executed in the order registered, with the first <c>Use</c> becoming the outermost component.
        /// </para>
        /// </summary>
        /// <param name="middleware">
        /// A delegate function of signature
        /// <c>Func&lt;TContext, PipelineDelegate&lt;TContext, TResult&gt;, CancellationToken, Task&lt;TResult&gt;&gt;</c>.
        /// </param>
        /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining of further registrations.</returns>
        IPipelineBuilder<TContext, TResult> Use(
            Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>> middleware
        );

        /// <summary>
        /// Registers a middleware component (an <see cref="IMiddleware{TContext, TResult}"/> implementation) in the pipeline.
        /// <para>
        /// The middleware instance’s <c>InvokeAsync</c> method will be called when its turn arrives. It can call the <paramref name="next"/> delegate
        /// to continue the pipeline or return a result directly to short‐circuit.
        /// </para>
        /// </summary>
        /// <param name="middleware">An object implementing <see cref="IMiddleware{TContext, TResult}"/>.</param>
        /// <returns>The same <see cref="IPipelineBuilder{TContext, TResult}"/> instance, allowing chaining of further registrations.</returns>
        IPipelineBuilder<TContext, TResult> Use(IMiddleware<TContext, TResult> middleware);

        /// <summary>
        /// Builds and composes all registered middleware into a single <see cref="PipelineDelegate{TContext, TResult}"/>.
        /// <para>
        /// Once <c>Build</c> is called, the pipeline becomes immutable—calling <c>Use</c> afterwards throws an <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// The returned delegate, when invoked, will execute each middleware in sequence. If no middleware short‐circuits,
        /// the terminal delegate simply returns <c>default(TResult)</c>.
        /// </para>
        /// </summary>
        /// <returns>
        /// A <see cref="PipelineDelegate{TContext, TResult}"/> that encapsulates the entire middleware chain.
        /// </returns>
        PipelineDelegate<TContext, TResult> Build();
    }
}