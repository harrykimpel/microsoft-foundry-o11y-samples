var llmChatCompletionMessageUser = new LlmChatCompletionMessage
{
    eventName = "LlmChatCompletionMessage",
    newrelicEventType = "LlmChatCompletionMessage",
    appName = ActivitySourceName,
    completion_id = completion.Id,
    content = userPrompt,
    host = Environment.MachineName,
    id = Guid.NewGuid().ToString(),
    ingest_source = "Dotnet",
    realAgentId = 1108190279,
    request_id = Guid.NewGuid().ToString(),
    response_model = deploymentName,
    role = "user",
    sequence = 0,
    trace_id = traceId,
    span_id = spanId,
    tags_account = "AI-Observability",
    tags_accountId = 4541509,
    tags_trustedAccountId = 3882521,
    timestamp = DateTime.UtcNow.ToString("o"),
    token_count = completion.Usage?.InputTokenCount,
    vendor = "openai"
};

// Build and log a payload with explicit dotted keys for downstream tools.
var newRelicPayloadUser = BuildLlmChatCompletionMessage(llmChatCompletionMessageUser);
LogNewRelicEvent(logger, newRelicPayloadUser, "LlmChatCompletionMessage");

// Create a complex object
var llmChatCompletionMessageAssistant = new LlmChatCompletionMessage
{
    eventName = "LlmChatCompletionMessage",
    newrelicEventType = "LlmChatCompletionMessage",
    appName = ActivitySourceName,
    completion_id = completion.Id,
    content = assistantMessageContent,
    host = Environment.MachineName,
    id = Guid.NewGuid().ToString(),
    ingest_source = "Dotnet",
    is_response = true,
    realAgentId = 1108190279,
    request_id = Guid.NewGuid().ToString(),
    response_model = deploymentName,
    role = "assistant",
    sequence = 1,
    span_id = spanId,
    trace_id = traceId,
    tags_account = "AI-Observability",
    tags_accountId = 4541509,
    tags_trustedAccountId = 3882521,
    token_count = completion.Usage?.OutputTokenCount,
    vendor = "openai"
};

// Build and log a payload with explicit dotted keys for downstream tools.
var newRelicPayloadAssistant = BuildLlmChatCompletionMessage(llmChatCompletionMessageAssistant);
LogNewRelicEvent(logger, newRelicPayloadAssistant, "LlmChatCompletionMessage");

// Create a complex object
var llmChatCompletionSummary = new LlmChatCompletionSummary
{
    eventName = "LlmChatCompletionSummary",
    newrelicEventType = "LlmChatCompletionSummary",
    appName = ActivitySourceName,
    duration = duration,
    host = Environment.MachineName,
    id = Guid.NewGuid().ToString(),
    ingest_source = "Dotnet",
    realAgentId = 1108190279,
    request_max_tokens = completion.Usage?.TotalTokenCount,
    request_model = deploymentName,
    request_temperature = "N/A",
    request_id = Guid.NewGuid().ToString(),
    response_choices_finish_reason = completion.FinishReason.ToString(),
    response_model = deploymentName,
    response_number_of_messages = "1",
    span_id = spanId,
    trace_id = traceId,
    tags_account = "AI-Observability",
    tags_accountId = 4541509,
    tags_trustedAccountId = 3882521,
    vendor = "openai"
};

// Build and log a payload with explicit dotted keys for downstream tools.
var newRelicPayloadSummary = BuildLlmChatCompletionSummary(llmChatCompletionSummary);
LogNewRelicEvent(logger, newRelicPayloadSummary, "LlmChatCompletionSummary");