namespace FlowSharp
{
    /// <summary>
    /// Defines a middleware component that can process a context and produce a result asynchronously.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">
    /// The type of the result produced by this middleware (and eventually by the pipeline).
    /// </typeparam>
    public interface IMiddleware<TContext, TResult>
    {
        /// <summary>
        /// Processes the specified <paramref name="context"/> asynchronously and optionally invokes the next middleware in the pipeline.
        /// </summary>
        /// <param name="context">The context object passed through the pipeline.</param>
        /// <param name="next">
        /// A delegate to invoke the next middleware in the pipeline. The returned <see cref="Task{TResult}"/> represents
        /// the result from downstream middleware or the terminal delegate.
        /// </param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that represents the asynchronous operation, containing the result of type <typeparamref name="TResult"/>.
        /// Middleware may return a value directly (short‐circuiting the pipeline) or await <paramref name="next"/> and possibly modify its result.
        /// </returns>
        Task<TResult> InvokeAsync(
            TContext context,
            PipelineDelegate<TContext, TResult> next,
            CancellationToken cancellationToken = default
        );
    }
}