#:package OpenAI@2.9.1
#:package OpenTelemetry.AutoInstrumentation@1.14.1
#:package OpenTelemetry.Instrumentation.Http@1.15.0
#:package OpenTelemetry.Instrumentation.GrpcCore@1.0.0-beta.10
#:package OpenTelemetry.Extensions.Hosting@1.15.0
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@1.15.0

using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;
using Microsoft.Extensions.Logging;
using System.Text.Json;

#pragma warning disable OPENAI001

const string deploymentName = "gpt-5-mini";
string endpoint = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_ENDPOINT") ?? "https://api.openai.com/v1";
string apiKey = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_API_KEY") ?? "YOUR_API_KEY_HERE";

string ActivitySourceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "chat-completion-sample";
const string ActivitySourceVersion = "1.0.0";
string OpenTelemetryExporterEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "https://otlp.nr-data.net:4317";
string OpenTelemetryApiKey = Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY") ?? "YOUR_NEW_RELIC_LICENSE_KEY_HERE";

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
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddConsoleExporter();
        logging.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(OpenTelemetryExporterEndpoint);
            options.Headers = $"api-key={OpenTelemetryApiKey}";
        });
    });
});

var logger = loggerFactory.CreateLogger("ChatCompletionSample");

ActivitySource activitySource = new ActivitySource(ActivitySourceName, ActivitySourceVersion);

using (var myMainActivity = activitySource.StartActivity("main", ActivityKind.Internal))
{
    myMainActivity?.SetTag("transaction.name", "main");
    myMainActivity?.SetTag("transaction.type", "custom");

    ChatClient client = new(
        credential: new ApiKeyCredential(apiKey),
        model: deploymentName,
        options: new OpenAIClientOptions()
        {
            Endpoint = new($"{endpoint}"),
        });

    using (var myChatActivity = activitySource.StartActivity("CompleteChat", ActivityKind.Internal))
    {
        myChatActivity?.SetTag("transaction.type", "custom");
        myChatActivity?.SetTag("transaction.name", "CompleteChat");

        string userPrompt = "What's the best way to train a parrot?";

        DateTime startTime = DateTime.UtcNow;
        ChatCompletion completion = await client.CompleteChatAsync(
        [
            new SystemChatMessage("You are a helpful assistant that talks like a pirate with a Boston accent."),
            new UserChatMessage("Hi, can you help me?"),
            new AssistantChatMessage("Arrr! Of course, me hearty! What can I do for ye?"),
            new UserChatMessage(userPrompt),
        ]);
        DateTime endTime = DateTime.UtcNow;
        double duration = (endTime - startTime).TotalMilliseconds;

        logger.LogInformation($"Model={completion.Model}");
        foreach (ChatMessageContentPart contentPart in completion.Content)
        {
            string message = contentPart.Text;
            string assistantMessageContent = string.Empty;
            logger.LogInformation($"Chat Role: {completion.Role}");
            if (message != null)
            {
                logger.LogInformation($"Message: {message}");
                // Log only the first 4000 characters to avoid excessively large logs
                assistantMessageContent = message.Substring(0, Math.Min(message.Length, 4000));
            }

            // get current trace and span ids for correlation with New Relic logs and metrics
            string? traceId = myChatActivity?.TraceId.ToString();
            string? spanId = myChatActivity?.SpanId.ToString();

            // TODO: Build and log a payload with explicit dotted keys for downstream tools

        }
    }
}

tracerProvider.Dispose();
meterProvider.Dispose();
loggerFactory.Dispose();

// ****************************************************
// NEW RELIC CUSTOM EVENT LOGGING HELPERS
// ****************************************************

// New Relic expects a flat key/value structure for custom events, so we build a payload with explicit dotted keys for downstream tools.
static void LogNewRelicEvent(ILogger logger, Dictionary<string, object?> payload, string EventType)
{
    // Log a flat key/value state bag so providers emit exact keys like
    // "event.name" and "newrelic.event.type" without object-name prefixes.
    logger.Log(
        LogLevel.Information,
        new EventId(1001, EventType),
        payload,
        exception: null,
        formatter: static (state, _) => JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        }));
}

static Dictionary<string, object?> BuildLlmChatCompletionMessage(in LlmChatCompletionMessage message)
{
    return new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["event.name"] = message.eventName,
        ["newrelic.event.type"] = message.newrelicEventType,
        ["appId"] = message.appId,
        ["appName"] = message.appName,
        ["completion_id"] = message.completion_id,
        ["content"] = message.content,
        ["gen_ai.prompt.0.content"] = message.content,
        ["gen_ai.completion.0.content"] = message.content,
        ["entityGuid"] = message.entityGuid,
        ["host"] = message.host,
        ["id"] = message.id,
        ["ingest_source"] = message.ingest_source,
        ["is_response"] = message.is_response,
        ["realAgentId"] = message.realAgentId,
        ["request_id"] = message.request_id,
        ["response.model"] = message.response_model,
        ["role"] = message.role,
        ["sequence"] = message.sequence,
        ["span_id"] = message.span_id,
        ["tags.account"] = message.tags_account,
        ["tags.accountId"] = message.tags_accountId,
        ["tags.trustedAccountId"] = message.tags_trustedAccountId,
        ["tags.aiEnabledApp"] = true,
        ["token_count"] = message.token_count,
        ["trace_id"] = message.trace_id,
        ["vendor"] = message.vendor
    };
}

static Dictionary<string, object?> BuildLlmChatCompletionSummary(in LlmChatCompletionSummary message)
{
    return new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["appId"] = message.appId,
        ["appName"] = message.appName,
        ["event.name"] = message.eventName,
        ["newrelic.event.type"] = message.newrelicEventType,
        ["duration"] = message.duration,
        ["entityGuid"] = message.entityGuid,
        ["host"] = message.host,
        ["id"] = message.id,
        ["ingest_source"] = message.ingest_source,
        ["realAgentId"] = message.realAgentId,
        ["request.max_tokens"] = message.request_max_tokens,
        ["request.model"] = message.request_model,
        ["request.temperature"] = message.request_temperature,
        ["request_id"] = message.request_id,
        ["response.choices.finish_reason"] = message.response_choices_finish_reason,
        ["response.model"] = message.response_model,
        ["response.number_of_messages"] = message.response_number_of_messages,
        ["span_id"] = message.span_id,
        ["tags.account"] = message.tags_account,
        ["tags.accountId"] = message.tags_accountId,
        ["tags.trustedAccountId"] = message.tags_trustedAccountId,
        ["tags.aiEnabledApp"] = true,
        ["trace_id"] = message.trace_id,
        ["vendor"] = message.vendor
    };
}

public partial struct LlmChatCompletionMessage
{
    public string? eventName { get; set; }
    public string? newrelicEventType { get; set; }
    public string? appId { get; set; }
    public string? appName { get; set; }
    public string? completion_id { get; set; }
    public string? content { get; set; }
    public string? entityGuid { get; set; }
    public string? host { get; set; }
    public string? id { get; set; }
    public string? ingest_source { get; set; }
    public bool? is_response { get; set; }
    public int? realAgentId { get; set; }
    public string? request_id { get; set; }
    public string? response_model { get; set; }
    public string? role { get; set; }
    public int? sequence { get; set; }
    public string? span_id { get; set; }
    public string? tags_account { get; set; }
    public int? tags_accountId { get; set; }
    public int? tags_trustedAccountId { get; set; }
    public string? timestamp { get; set; }
    public long? token_count { get; set; }
    public string? trace_id { get; set; }
    public string? vendor { get; set; }
}

public partial struct LlmChatCompletionSummary
{
    public string? eventName { get; set; }
    public string? newrelicEventType { get; set; }
    public string? appId { get; set; }
    public string? appName { get; set; }
    public double? duration { get; set; }
    public string? host { get; set; }
    public string? id { get; set; }
    public string? entityGuid { get; set; }
    public string? ingest_source { get; set; }
    public int? realAgentId { get; set; }
    public long? request_max_tokens { get; set; }
    public string? request_model { get; set; }
    public string? request_temperature { get; set; }
    public string? request_id { get; set; }
    public string? response_choices_finish_reason { get; set; }
    public string? response_model { get; set; }
    public string? response_number_of_messages { get; set; }
    public string? span_id { get; set; }
    public string? tags_account { get; set; }
    public int? tags_accountId { get; set; }
    public int? tags_trustedAccountId { get; set; }
    public string? timestamp { get; set; }
    public string? trace_id { get; set; }
    public string? vendor { get; set; }
}