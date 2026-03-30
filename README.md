# Microsoft Foundry O11y Samples

This repository contains a collection of observability and AI agent samples using Microsoft Foundry, OpenTelemetry, and related frameworks. The samples demonstrate best practices for instrumenting, monitoring, and building intelligent applications in .NET and Python.

## Contents

### [agent-framework-travel-planner](./agent-framework-travel-planner) for a travel planning application using Microsoft Agent Framework, OpenTelemetry, and New Relic APM. Includes a .NET backend API and Blazor frontend

- [dotnet](./agent-framework-travel-planner/dotnet/): .NET Aspire solution with multiple projects:
  - **AspireApp.ApiService**: Backend API with OpenTelemetry tracing and AI agent integration.
  - **AspireApp.Web**: Blazor web frontend for travel planning, calling the API and showing weather/itinerary.
  - **AspireApp.ServiceDefaults**: Shared service configuration.
  - **AspireApp.Tests**: Test project for the solution.
- [python](./agent-framework-travel-planner/python/): Flask web app with Microsoft Agent Framework, OpenTelemetry, and a security demonstration mode. Includes a visual guide and templates for attack/defense scenarios.

### chat-completion

- [newrelic](./chat-completion/newrelic/): .NET sample showing OpenAI chat completions with New Relic APM instrumentation.
- [otel](./chat-completion/otel/): .NET sample for OpenAI chat completions with OpenTelemetry instrumentation.

### [multiple-embeddings](./multiple-embeddings/)

- .NET sample for generating and tracing multiple OpenAI embeddings with OpenTelemetry.

### [realtime](./realtime)

- .NET sample for real-time AI agent interactions, audio analysis, audio generation,weather API integration, and OpenTelemetry tracing.

---

Each sample demonstrates how to combine AI agent capabilities with robust observability using OpenTelemetry, New Relic, and Microsoft Foundry. See individual folders for setup and usage instructions.
