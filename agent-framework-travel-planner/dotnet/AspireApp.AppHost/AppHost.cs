var builder = DistributedApplication.CreateBuilder(args);

var NEW_RELIC_REGION = Environment.GetEnvironmentVariable("NEW_RELIC_REGION");
string OTEL_EXPORTER_OTLP_ENDPOINT = "https://otlp.nr-data.net";
if (NEW_RELIC_REGION != null &&
    NEW_RELIC_REGION != "" &&
    NEW_RELIC_REGION == "EU")
{
    OTEL_EXPORTER_OTLP_ENDPOINT = "https://otlp.eu01.nr-data.net";
}
var NEW_RELIC_LICENSE_KEY = Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY");
string OTEL_EXPORTER_OTLP_HEADERS = "api-key=" + NEW_RELIC_LICENSE_KEY;

var MSFT_FOUNDRY_ENDPOINT = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_ENDPOINT");
var MSFT_FOUNDRY_API_KEY = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_API_KEY");
var MSFT_FOUNDRY_DEPLOYMENT_NAME = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_DEPLOYMENT_NAME");

var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("MSFT_FOUNDRY_ENDPOINT", MSFT_FOUNDRY_ENDPOINT)
    .WithEnvironment("MSFT_FOUNDRY_API_KEY", MSFT_FOUNDRY_API_KEY)
    .WithEnvironment("MSFT_FOUNDRY_DEPLOYMENT_NAME", MSFT_FOUNDRY_DEPLOYMENT_NAME)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", OTEL_EXPORTER_OTLP_ENDPOINT)
    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", OTEL_EXPORTER_OTLP_HEADERS)
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "true")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_METADATA", "true")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_TOOL_OUTPUT", "true")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_TOOL_INPUT", "true")
    .WithEnvironment("OTEL_PYTHON_LOGGING_AUTO_INSTRUMENTATION_ENABLED", "true")
    .WithEnvironment("ENABLE_OTEL", "true")
    .WithEnvironment("ENABLE_SENSITIVE_DATA", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "aspire-apiservice")
    .WithEnvironment("OTEL_SEMCONV_STABILITY_OPT_IN", "http/dup,database/dup,genai,gen_ai_latest_experimental")
    .WithEnvironment("OPENAI_EXPERIMENTAL_ENABLE_OPEN_TELEMETRY", "true");

builder.AddProject<Projects.AspireApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", OTEL_EXPORTER_OTLP_ENDPOINT)
    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", OTEL_EXPORTER_OTLP_HEADERS)
    .WithEnvironment("OTEL_SERVICE_NAME", "aspire-webfrontend")
    .WaitFor(apiService);

builder.Build().Run();
