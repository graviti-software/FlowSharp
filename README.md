# FlowSharp

A lightweight, in-place pipeline framework for .NET 8 and later. FlowSharp provides both:

1. A **static helper** (`PipelineBuilder.RunAsync`) for building and running middleware pipelines without any dependency‐injection (DI) container.
2. A **DI‐friendly implementation** (`IPipelineBuilder<TContext, TResult>` → `PipelineBuilder<TContext, TResult>`) that can be registered in an `IServiceCollection` and used in ASP.NET Core or any other DI‐enabled host.

---

## Table of Contents

1. [Overview](#overview)
2. [Features](#features)
3. [Prerequisites](#prerequisites)
4. [Installation](#installation)
5. [Usage](#usage)

   * [Without DI (Static Helper)](#without-di-static-helper)
   * [With DI (Service Collection)](#with-di-service-collection)
6. [Project Structure](#project-structure)
7. [Contributing](#contributing)
8. [License](#license)

---

## Overview

FlowSharp is a minimal, open‐generic “pipeline” framework inspired by classic middleware concepts (similar to ASP.NET Core’s request pipeline) but designed to be entirely standalone. In its simplest form, FlowSharp lets you:

- Register an ordered list of “middleware” delegates or middleware classes
- Pass a `TContext` object and a `CancellationToken` through all middleware
- Eventually invoke a final “root action” that produces a `TResult`

You can choose to use FlowSharp in two modes:

1. **Static mode** (no DI required): call `PipelineBuilder.RunAsync<TContext, TResult>(...)` directly anywhere.
2. **DI mode** (with DI container): register the open‐generic `IPipelineBuilder<,>` → `PipelineBuilder<,>` mapping, then resolve `IPipelineBuilder<MyContext, MyResult>` from your container, call `Use(...)` for each middleware, and finally call `Build()`.

---

## Features

- **Zero external dependencies** (only uses .NET 8 built-in libraries).  
- **Open-generic DI registration**: register `IPipelineBuilder<,>` once and it works for any `TContext, TResult`.  
- **Inline middleware support**: pass in a `Func<TContext, PipelineDelegate<TContext, TResult>, CancellationToken, Task<TResult>>` delegate without creating a full class.  
- **Thread-safe building**: once you begin execution, no further middleware can be registered.  
- **Seamless “root action” wiring**: you specify your terminal handler (the final `TResult` producer) as part of your pipeline.  
- **Easy unit‐testing**: no container or special host is required; you can build a pipeline and immediately invoke it.

---

## Prerequisites

- .NET 8 SDK or later  
- Any IDE or editor that supports C# 10+ (Visual Studio 2022/2023, Rider, Visual Studio Code, etc.)  
- (Optional, for DI mode) Any `IServiceCollection`‐compatible container (e.g., Microsoft.Extensions.DependencyInjection)

---

## Installation

Clone the repository, add a reference to `FlowSharp`, or create a NuGet package:

```bash
dotnet add package FlowSharp --version 1.0.0
```

---

## Usage

### Without DI (Static Helper)

```csharp
var result = await PipelineBuilder.RunAsync(rootAction, context, middlewares[], CancellationToken);
```

### With DI (Service Collection)

```csharp
services.AddFlowSharp();
var pipeline = provider.GetRequiredService<IPipelineBuilder<MyContext, MyResult>>();
pipeline.Use(middleware1).Use(middleware2);
var flow = await pipeline.Build();
MyContext context = ...; // Create your context
MyResult result = await flow.Invoke(context, ct);
```

---

## Project Structure

```
/src
  └── FlowSharp
      ├── DependencyInjection.cs
      ├── IPipelineBuilder.cs
      ├── IMiddleware.cs
      ├── PipelineDelegate.cs
      ├── PipelineBuilder.cs

/tests
  └── FlowSharp.Tests

/examples
  └── ConsoleApp
```

---

## Contributing

* Open issues for discussions.
* Fork and branch your work.
* Include tests for new features.
* Follow existing coding styles.
* Submit PRs clearly describing changes.

---

## License

FlowSharp is licensed under the **MIT License**. See [LICENSE](LICENSE) for full details.
