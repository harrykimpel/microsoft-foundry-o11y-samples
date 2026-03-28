#:package OpenAI@2.9.1
#:package OpenTelemetry.Instrumentation.Http@1.15.0
#:package OpenTelemetry.Extensions.Hosting@1.15.0
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@1.15.0

using OpenAI.Embeddings;
using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;
using Microsoft.Extensions.Logging;


string? apiKey = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_API_KEY");
string? endpoint = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_ENDPOINT");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("MSFT_FOUNDRY_API_KEY is not set. Please set it and run again.");
    return;
}

if (string.IsNullOrWhiteSpace(endpoint))
{
    Console.Error.WriteLine("MSFT_FOUNDRY_ENDPOINT is not set. Please set it and run again.");
    return;
}

const string ActivitySourceName = "multiple-embeddings";
const string ActivitySourceVersion = "1.0.0";
string OpenTelemetryExporterEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "https://otlp.nr-data.net";
string OpenTelemetryApiKey = Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY") ?? "YOUR_NEW_RELIC_LICENSE_KEY_HERE";

ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: ActivitySourceName, serviceVersion: ActivitySourceVersion);

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("OpenAI.*")
    .AddSource(ActivitySourceName)
    .ConfigureResource(resource =>
        resource.AddService(
          serviceName: ActivitySourceName,
          serviceVersion: ActivitySourceVersion))
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(OpenTelemetryExporterEndpoint);
        options.Headers = $"api-key={OpenTelemetryApiKey}";
    })
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("OpenAI.*")
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(OpenTelemetryExporterEndpoint);
        options.Headers = $"api-key={OpenTelemetryApiKey}";
    })
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(resourceBuilder);
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.ParseStateValues = true;

                logging.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(OpenTelemetryExporterEndpoint);
                    options.Headers = $"api-key={OpenTelemetryApiKey}";
                });
            });
        });

// Get a logger instance
var logger = loggerFactory.CreateLogger("MultipleEmbeddingsExample");

ActivitySource activitySource = new ActivitySource(ActivitySourceName, ActivitySourceVersion);

using (var myMainActivity = activitySource.StartActivity("main", ActivityKind.Internal))
{
    System.ClientModel.ApiKeyCredential credential = new(apiKey);
    OpenAI.OpenAIClientOptions options = new OpenAI.OpenAIClientOptions()
    {
        Endpoint = new Uri(endpoint)
    };

    EmbeddingClient client = new("text-embedding-3-small", credential, options);

    string category = "Luxury";
    string description = "Best hotel in town if you like luxury hotels. They have an amazing infinity pool, a spa,"
        + " and a really helpful concierge. The location is perfect -- right downtown, close to all the tourist"
        + " attractions. We highly recommend this hotel.";
    List<string> inputs = new() { category, description };

    OpenAIEmbeddingCollection collection = await client.GenerateEmbeddingsAsync(inputs);

    foreach (OpenAIEmbedding embedding in collection)
    {
        ReadOnlyMemory<float> vector = embedding.ToFloats();

        logger.LogInformation($"Dimension: {vector.Length}");
        logger.LogInformation("Floats:");
        for (int i = 0; i < vector.Length; i++)
        {
            logger.LogInformation($"  [{i,4}] = {vector.Span[i]}");
        }

        logger.LogInformation("");
    }
}

tracerProvider.Dispose();
meterProvider.Dispose();
loggerFactory.Dispose();