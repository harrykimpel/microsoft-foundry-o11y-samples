using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.Json;
using System.Diagnostics;

const string SourceName = "OpenTelemetryAspire.TravelPlannerApp";
var ServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "TravelPlannerApp";

// Configure OpenTelemetry for Aspire dashboard
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318";
var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");

// Create a resource to identify this service
var resource = ResourceBuilder.CreateDefault()
    .AddService(ServiceName, serviceVersion: "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["service.instance.id"] = Environment.MachineName,
        ["deployment.environment"] = "development"
    })
    .Build();

// Setup tracing with resource
var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName, serviceVersion: "1.0.0"))
    .AddSource(SourceName) // Our custom activity source
    .AddSource("*Microsoft.Agents.AI") // Agent Framework telemetry
    .AddHttpClientInstrumentation() // Capture HTTP calls to OpenAI
    .AddOtlpExporter(
        options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Headers = otlpHeaders;
        }
        );

using var tracerProvider = tracerProviderBuilder.Build();

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var endpoint = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_ENDPOINT")?.Trim() ?? throw new InvalidOperationException("MSFT_FOUNDRY_ENDPOINT is not set.");
endpoint = endpoint.Replace("/openai/v1/", ""); // Remove path if user set the endpoint to the full URL by mistake, we only need the base URL for AzureOpenAIClient.)
var endpointAPIKey = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_API_KEY")?.Trim() ?? throw new InvalidOperationException("MSFT_FOUNDRY_API_KEY is not set.");
var deploymentName = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_DEPLOYMENT_NAME")?.Trim();
if (string.IsNullOrWhiteSpace(deploymentName))
{
    throw new InvalidOperationException(
        "MSFT_FOUNDRY_DEPLOYMENT_NAME is not set. Use the Azure OpenAI deployment name (user-defined), not the base model name.");
}
var agentTimeoutSeconds = builder.Configuration.GetValue<int?>("Foundry:AgentTimeoutSeconds") ?? 120;
var networkTimeoutSeconds = builder.Configuration.GetValue<int?>("Foundry:NetworkTimeoutSeconds") ?? agentTimeoutSeconds;
var enableSensitiveData = Environment.GetEnvironmentVariable("ENABLE_SENSITIVE_DATA")?.Trim() == "true";

if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
{
    throw new InvalidOperationException("MSFT_FOUNDRY_ENDPOINT is not a valid absolute URI.");
}

if (!endpointUri.Host.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "MSFT_FOUNDRY_ENDPOINT must be an Azure OpenAI endpoint like https://<resource>.openai.azure.com/. " +
        "A Foundry project endpoint will return 404 with AzureOpenAIClient.");
}

if (endpointUri.AbsolutePath is not "/" and not "" || !string.IsNullOrEmpty(endpointUri.Query))
{
    throw new InvalidOperationException(
        "MSFT_FOUNDRY_ENDPOINT must be the base resource URL only (for example, https://<resource>.openai.azure.com/). " +
        "Do not include paths like /openai/deployments/... or query parameters.");
}

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

Dictionary<string, string> destinations = new(StringComparer.OrdinalIgnoreCase)
{
    ["Garmisch-Partenkirchen, Germany"] = "🏔️ Alpine village with stunning mountain views",
    ["Munich, Germany"] = "🍺 Bavarian capital famous for culture and beer",
    ["Berlin, Germany"] = "🎨 Historic and vibrant cultural hub",
    ["Rome, Italy"] = "🏛️ Ancient city with rich history and art",
    ["Barcelona, Spain"] = "🏖️ Coastal city with stunning architecture",
    ["Boston, USA"] = "🍀 Historic city with rich colonial heritage",
    ["New York, USA"] = "🗽 The city that never sleeps",
    ["Tokyo, Japan"] = "🗾 Bustling metropolis with ancient temples",
    ["Sydney, Australia"] = "🦘 Opera House and beautiful beaches",
    ["Cairo, Egypt"] = "🔺 Gateway to ancient wonders",
    ["Cape Town, South Africa"] = "🌅 Scenic beauty and Table Mountain",
    ["Rio de Janeiro, Brazil"] = "🎭 Vibrant culture and beaches",
    ["Bali, Indonesia"] = "🌴 Tropical paradise and spiritual haven",
    ["Paris, France"] = "🗼 The City of Light, romantic and iconic"
};

// Create the chat client and agent, and provide the function tool to the agent.
// WARNING: DefaultAzureCredential is convenient for development but requires careful consideration in production.
// In production, consider using a specific credential (e.g., ManagedIdentityCredential) to avoid
// latency issues, unintended credential probing, and potential security risks from fallback mechanisms.
var instrumentedChatClient = new AzureOpenAIClient(
    endpointUri, new ApiKeyCredential(endpointAPIKey))
    .GetChatClient(deploymentName)
    .AsIChatClient() // Converts a native OpenAI SDK ChatClient into a Microsoft.Extensions.AI.IChatClient
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "aspire-apiservice", configure: (cfg) => cfg.EnableSensitiveData = enableSensitiveData)    // Enable OpenTelemetry instrumentation with sensitive data
    .Build();

// Create the main agent, and provide the weather, random destination, and date/time as function tools.
var agent = new ChatClientAgent(
        instrumentedChatClient,
        name: "Travel-Planner-Agent",
        instructions: "You are a helpful assistant that provides concise and informative responses.",
        tools: [
            AIFunctionFactory.Create(GetWeather),
            AIFunctionFactory.Create(GetRandomDestination),
            AIFunctionFactory.Create(GetDateTime)]
    ).AsBuilder()
    .UseOpenTelemetry(SourceName, configure: (cfg) => cfg.EnableSensitiveData = enableSensitiveData) // enable telemetry at the agent level
    .Build();

// Tool function: pick a random destination from a predefined list.
async Task<string> GetRandomDestination(CancellationToken cancellationToken)
{
    // Simulate network latency with a small random delay.
    var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 990));
    await Task.Delay(delay, cancellationToken);

    var destinationNames = destinations.Keys.ToArray();
    var selectedDestination = destinationNames[Random.Shared.Next(destinationNames.Length)];
    //Console.WriteLine($"[get_destination_from_list] selected {selectedDestination}");
    app.Logger.LogInformation("Selected random destination: {SelectedDestination}", selectedDestination);

    return selectedDestination;
}

// Tool Function: Get weather forecast
async Task<WeatherForecast[]> GetWeather(string location, string startDate, int duration, CancellationToken cancellationToken)
{
    // In a real implementation, you would call an external weather API here.
    // For this example, we'll return mock data.
    var rng = new Random();
    WeatherForecast[] forecasts = Enumerable.Range(1, duration).Select(index => new WeatherForecast
    (
        DateOnly.FromDateTime(DateTime.Parse(startDate).AddDays(index - 1)),
        rng.Next(-20, 55),
        summaries[rng.Next(summaries.Length)]
    )).ToArray();
    app.Logger.LogInformation("Generated weather forecast for {Location} starting on {StartDate} for {Duration} days.", location, startDate, duration);
    return forecasts;
}

// Tool Function: Get current date and time
async Task<string> GetDateTime(CancellationToken cancellationToken)
{
    // Simulate network latency with a random delay.
    var delay = TimeSpan.FromMilliseconds(Random.Shared.Next(100, 5001));
    await Task.Delay(delay, cancellationToken);

    string currentDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    app.Logger.LogInformation("Current date and time: {CurrentDateTime}", currentDateTime);
    return currentDateTime;
}

// Define the API endpoint for creating a travel plan. The agent is invoked to generate the itinerary based on the request parameters.
app.MapPost("/travelplan", async (TravelPlanRequest request, CancellationToken cancellationToken) =>
{
    app.Logger.LogInformation("Received travel plan request for {TravelerName} starting on {StartDate} for {Nights} nights.",
        request.TravelerName, request.StartDate, request.Nights);

    if (request.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow))
    {
        app.Logger.LogError("Start date must be in the future. Received start date: {StartDate}", request.StartDate);
        return Results.BadRequest("Start date must be in the future.");
    }

    var nights = Math.Max(1, request.Nights);

    // Invoke the agent with a configurable timeout while respecting request cancellation.
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(agentTimeoutSeconds));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

    string suggestions =
    @"- Visit local landmarks and museums
      - Try a neighborhood food tour
      - Reserve one day for a relaxed itinerary";

    var itinerary = string.Empty;

    try
    {
        var duration = (request.Nights + 1).ToString();
        var date = request.StartDate.ToString("yyyy-MM-dd");
        string userPrompt = $@"Plan me a {duration}-day trip to a random destination starting on {date}.

            Trip Details:
                - Date: {date}
                - Duration: {duration} days
                - Interests: 
                  {suggestions}

            Instructions:
                1. A detailed day-by-day itinerary with activities tailored to the interests
                2. Current weather information for the destination
                3. Local cuisine recommendations
                4. Best times to visit specific attractions
                5. Travel tips and budget estimates
                6. Current date and time reference";

        app.Logger.LogInformation("Invoking agent with prompt: {UserPrompt}", userPrompt);
        DateTime agentStartTime = DateTime.UtcNow;
        var agentResponse = await agent.RunAsync(
            userPrompt,
            session: null,
            options: null,
            cancellationToken: linkedCts.Token);
        DateTime agentEndTime = DateTime.UtcNow;
        TimeSpan agentDuration = agentEndTime - agentStartTime;
        itinerary = agentResponse.Text;
        app.Logger.LogInformation("Agent response received successfully.");

        var completionId = Guid.NewGuid().ToString();
        var requestId = Guid.NewGuid().ToString();

        // Store the current activity to restore it later
        var currentActivity = Activity.Current;

        var traceId = currentActivity?.TraceId.ToString();
        var spanId = currentActivity?.SpanId.ToString();

        // Create a complex object
        var llmChatCompletionMessageUser = new LlmChatCompletionMessage
        {
            eventName = "LlmChatCompletionMessage",
            newrelicEventType = "LlmChatCompletionMessage",
            appId = "AspireApp.ApiService",
            appName = ServiceName,
            completion_id = completionId,
            content = userPrompt,
            entityGuid = "NDU0MTUwOXxFWFR8U0VSVklDRXw0MTg0NTQ5NDYzMDI3NzU5NjU0",
            host = Environment.MachineName,
            id = Guid.NewGuid().ToString(),
            ingest_source = "AzureOpenAIClient",
            //is_response = false,
            realAgentId = Guid.NewGuid().ToString(),
            request_id = requestId,
            response_model = deploymentName,
            role = "user",
            sequence = 0,
            trace_id = traceId,
            span_id = spanId,
            tags_account = "AI-Observability",
            tags_accountId = 4541509,
            tags_trustedAccountId = 3882521,
            timestamp = DateTime.UtcNow.ToString("o"),
            token_count = agentResponse.Usage?.InputTokenCount,
            vendor = "Microsoft"
        };

        // Build and log a payload with explicit dotted keys for downstream tools.
        var newRelicPayloadUser = BuildLlmChatCompletionMessage(llmChatCompletionMessageUser);
        LogNewRelicEvent(app.Logger, newRelicPayloadUser, "LlmChatCompletionMessage");

        // Create a complex object
        var llmChatCompletionMessageAssistant = new LlmChatCompletionMessage
        {
            eventName = "LlmChatCompletionMessage",
            newrelicEventType = "LlmChatCompletionMessage",
            appId = "AspireApp.ApiService",
            appName = ServiceName,
            completion_id = completionId,
            content = itinerary.Substring(0, Math.Min(itinerary.Length, 4000)), // Log only the first 1000 characters to avoid excessively large logs
            entityGuid = "NDU0MTUwOXxFWFR8U0VSVklDRXw0MTg0NTQ5NDYzMDI3NzU5NjU0",
            host = Environment.MachineName,
            id = Guid.NewGuid().ToString(),
            ingest_source = "AzureOpenAIClient",
            is_response = true,
            realAgentId = Guid.NewGuid().ToString(),
            request_id = requestId,
            response_model = deploymentName,
            role = "assistant",
            sequence = 1,
            span_id = spanId,
            trace_id = traceId,
            tags_account = "AI-Observability",
            tags_accountId = 4541509,
            tags_trustedAccountId = 3882521,
            token_count = agentResponse.Usage?.OutputTokenCount,
            vendor = "Microsoft"
        };

        // Build and log a payload with explicit dotted keys for downstream tools.
        var newRelicPayloadAssistant = BuildLlmChatCompletionMessage(llmChatCompletionMessageAssistant);
        LogNewRelicEvent(app.Logger, newRelicPayloadAssistant, "LlmChatCompletionMessage");

        // Create a complex object
        var llmChatCompletionSummary = new LlmChatCompletionSummary
        {
            eventName = "LlmChatCompletionSummary",
            newrelicEventType = "LlmChatCompletionSummary",
            appId = "AspireApp.ApiService",
            appName = ServiceName,
            duration = agentDuration.TotalSeconds,
            host = Environment.MachineName,
            entityGuid = "NDU0MTUwOXxFWFR8U0VSVklDRXw0MTg0NTQ5NDYzMDI3NzU5NjU0",
            id = Guid.NewGuid().ToString(),
            ingest_source = "AzureOpenAIClient",
            realAgentId = Guid.NewGuid().ToString(),
            request_max_tokens = agentResponse.Usage?.TotalTokenCount,
            request_model = deploymentName,
            request_temperature = "N/A",
            request_id = Guid.NewGuid().ToString(),
            response_choices_finish_reason = agentResponse.FinishReason.ToString(),
            response_model = deploymentName,
            response_number_of_messages = "1",
            span_id = spanId,
            trace_id = traceId,
            tags_account = "AI-Observability",
            tags_accountId = 4541509,
            tags_trustedAccountId = 3882521,
            vendor = "Microsoft"
        };

        // Build and log a payload with explicit dotted keys for downstream tools.
        var newRelicPayloadSummary = BuildLlmChatCompletionSummary(llmChatCompletionSummary);
        LogNewRelicEvent(app.Logger, newRelicPayloadSummary, "LlmChatCompletionSummary");
    }
    catch (ClientResultException ex) when (ex.Status == 404)
    {
        app.Logger.LogError(ex,
            "OpenAI returned 404. Endpoint host: {EndpointHost}, deployment: {DeploymentName}.",
            endpointUri.Host,
            deploymentName);

        return Results.Problem(
            detail: "OpenAI returned 404 (Resource not found). The endpoint host exists, so the most likely issue is deployment resolution: verify MSFT_FOUNDRY_DEPLOYMENT_NAME is your Azure OpenAI deployment name (user-defined), not a model label like gpt-5-mini. Also verify that deployment supports chat completions with this SDK.",
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
    {
        return Results.Problem(
            detail: $"The travel planning agent call timed out after {agentTimeoutSeconds} seconds.",
            statusCode: StatusCodes.Status504GatewayTimeout);
    }

    return Results.Ok(new TravelPlanResponse(
        request.StartDate,
        nights,
        $"Mock plan generated for {request.TravelerName}.",
        itinerary));
})
.WithName("CreateTravelPlan");

app.MapGet("/weatherforecast", async () =>
{
    var weather = await GetWeather("Seattle", DateTime.UtcNow.ToString("yyyy-MM-dd"), 5, CancellationToken.None);
    return weather;
})
.WithName("GetWeatherForecast");

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
        ["trace_id"] = message.trace_id,
        ["vendor"] = message.vendor
    };
}

static void LogNewRelicEvent(ILogger logger, Dictionary<string, object?> payload, string EventType)
{
    // Log a flat key/value state bag so providers emit exact keys like
    // "event.name" and "newrelic.event.type" without object-name prefixes.
    logger.Log(
        LogLevel.Information,
        new EventId(1001, EventType),
        payload,
        exception: null,
        formatter: static (state, _) => JsonSerializer.Serialize(state));
}

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record TravelPlanRequest(string TravelerName, DateOnly StartDate, int Nights);

record TravelPlanResponse(
    DateOnly StartDate,
    int Nights,
    string Note,
    string Itinerary);

record TravelPlanDay(DateOnly Date, string Activity);

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
    public string? realAgentId { get; set; }
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
    public string? realAgentId { get; set; }
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