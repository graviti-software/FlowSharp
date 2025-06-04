namespace FlowSharp
{
    /// <summary>
    /// Represents an asynchronous delegate for executing a pipeline step.
    /// </summary>
    /// <typeparam name="TContext">The type of the context object passed through the pipeline.</typeparam>
    /// <typeparam name="TResult">
    /// The type of the result produced by the pipeline execution.
    /// Note: If <typeparamref name="TResult"/> is a reference type, returning <c>default(TResult)</c> in the terminal delegate means <c>null</c>.
    /// If <typeparamref name="TResult"/> is a value type, returning <c>default(TResult)</c> produces the zero‐equivalent (e.g., 0 for <c>int</c>).
    /// </typeparam>
    /// <param name="context">The context instance containing data relevant to the pipeline step.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete, enabling cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation, containing the result of the pipeline step.
    /// </returns>
    public delegate Task<TResult> PipelineDelegate<TContext, TResult>(
        TContext context,
        CancellationToken cancellationToken
    );
}