using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowSharp
{
    /// <summary>
    /// Extension methods for registering the pipeline builder in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// Adds the open‐generic <see cref="IPipelineBuilder{TIn, TOut}"/> → <see cref="PipelineBuilder{TIn, TOut}"/> mapping
        /// to the <see cref="IServiceCollection"/> using <see cref="ServiceCollectionDescriptorExtensions.TryAdd"/>.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="serviceLifetime">
        /// The <see cref="ServiceLifetime"/> for <see cref="IPipelineBuilder{TContext, TResult}"/>.
        /// Defaults to <see cref="ServiceLifetime.Scoped"/>.
        /// Use <c>Transient</c> if you prefer a new builder per resolution, or <c>Singleton</c> if you want one builder for the entire application (not generally recommended).
        /// </param>
        public static void AddFlowSharp(
            this IServiceCollection services,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped
        )
        {
            var descriptor = new ServiceDescriptor(
                typeof(IPipelineBuilder<,>),
                typeof(PipelineBuilder<,>),
                serviceLifetime
            );

            services.TryAdd(descriptor);
        }
    }
}