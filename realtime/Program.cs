#:package OpenAI@2.9.1
#:package OpenTelemetry.AutoInstrumentation@1.14.1
#:package OpenTelemetry.Instrumentation.Http@1.15.0
#:package OpenTelemetry.Instrumentation.GrpcCore@1.0.0-beta.10
#:package OpenTelemetry.Extensions.Hosting@1.15.0
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@1.15.0

using OpenAI.Realtime;
using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenAI.Examples;

#pragma warning disable OPENAI002

public static class RealtimeExamples
{
    private static ILogger logger = NullLogger.Instance;

    private static string GetCurrentWeather(string location, string unit = "celsius")
    {
        // Call the weather API here.
        var random = new Random();
        int temperature = random.Next(-10, 35);
        var weather = $"{temperature} {unit}";
        logger.LogInformation($"Getting current weather for {location}: {weather}");
        return weather;
    }

    private static readonly RealtimeFunctionTool getCurrentWeatherTool = new(functionName: nameof(GetCurrentWeather))
    {
        FunctionDescription = "gets the weather for a location",
        FunctionParameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "location": {
                        "type": "string",
                        "description": "The city and state, e.g. Boston, MA"
                    },
                    "unit": {
                        "type": "string",
                        "enum": [ "celsius", "fahrenheit" ],
                        "description": "The temperature unit to use. Infer this from the specified location."
                    }
                },
                "required": [ "location" ]
            }
            """)
    };

    private static string GetDateTime(string location)
    {
        // Call the datetime API here.
        var dateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        logger.LogInformation($"Getting current date and time for {location}: {dateTime}");
        return dateTime;
    }

    private static readonly RealtimeFunctionTool getCurrentDateTimeTool = new(functionName: nameof(GetDateTime))
    {
        FunctionDescription = "gets the current date and time for a location",
        FunctionParameters = BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "location": {
                        "type": "string",
                        "description": "The city and state, e.g. Boston, MA"
                    }
                },
                "required": [ "location" ]
            }
            """)
    };

    public static async Task RunAsync(ILogger loggerMain, ActivitySource activitySource)
    {
        using (var myMainActivity = activitySource.StartActivity("RunAsync", ActivityKind.Internal))
        {
            logger = loggerMain ?? throw new ArgumentNullException(nameof(loggerMain));

            string endpoint = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_ENDPOINT_2") ?? throw new InvalidOperationException("Endpoint not found in environment variables.");
            string apiKey = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_API_KEY_2") ?? throw new InvalidOperationException("API key not found in environment variables.");
            System.ClientModel.ApiKeyCredential credential = new(apiKey);
            RealtimeClientOptions options = new RealtimeClientOptions()
            {
                Endpoint = new Uri(endpoint)
            };

            RealtimeClient client = new(credential, options);

            using RealtimeSessionClient sessionClient = await client.StartConversationSessionAsync(model: "gpt-realtime");

            RealtimeConversationSessionOptions sessionOptions = new()
            {
                Instructions = "You are a cheerful assistant that talks like a pirate. "
                    + "Always inform the user when you are about to call a tool. "
                    + "Prefer to call tools whenever applicable.",

                Tools = { getCurrentWeatherTool, getCurrentDateTimeTool },

                AudioOptions = new()
                {
                    InputAudioOptions = new()
                    {
                        // AudioFormat = new GARealtimePcmAudioFormat(),
                        AudioTranscriptionOptions = new()
                        {
                            Model = "gpt-4o-transcribe",
                        },
                        TurnDetection = new RealtimeServerVadTurnDetection(),
                    },
                    OutputAudioOptions = new()
                    {
                        // AudioFormat = new GARealtimePcmAudioFormat(),
                        Voice = RealtimeVoice.Alloy,
                    },
                },
            };

            await sessionClient.ConfigureConversationSessionAsync(sessionOptions);

            using (var conversationActivity = activitySource.StartActivity("Conversation", ActivityKind.Internal))
            {
                // The conversation history (if applicable) can be provided by adding messages to the
                // conversation one by one. Note that adding a message will not automatically initiate
                // a response from the model.
                var userMessage =
                    "I'm trying to decide what to wear on my trip. "
                    + "Make a fun statement about the `Boston Code Camp` 40th edition that I am attending. "
                    + "Then, get the current date/time in Boston, MA and include that in your response.";
                await sessionClient.AddItemAsync(RealtimeItem.CreateUserMessageItem(userMessage));

                // using (var sendInputActivity = activitySource.StartActivity("SendInput", ActivityKind.Internal))
                // {
                //string inputAudioFilePath = Path.Join("Assets", "realtime_whats_the_weather_pcm16_24khz_mono.wav");
                string inputAudioFilePath = Path.Join("Assets", "realtime-weather-bos-2_pcm16_24khz_mono.wav");
                using Stream inputAudioStream = File.OpenRead(inputAudioFilePath);
                _ = sessionClient.SendInputAudioAsync(inputAudioStream);
                // }

                string outputAudioFilePath = Path.Join("Output", "output.raw");
                using Stream outputAudioStream = File.OpenWrite(outputAudioFilePath);

                bool done = false;

                await foreach (RealtimeServerUpdate update in sessionClient.ReceiveUpdatesAsync())
                {
                    using (var receivedUpdateActivity = activitySource.StartActivity("ReceivedUpdate." + update.GetType().Name, ActivityKind.Internal))
                    {
                        switch (update)
                        {
                            case RealtimeServerUpdateSessionCreated sessionCreatedUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", sessionCreatedUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {sessionCreatedUpdate.EventId}]");
                                    logger.LogInformation($">> Session created.");

                                    break;
                                }
                            case RealtimeServerUpdateSessionUpdated sessionUpdatedUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", sessionUpdatedUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {sessionUpdatedUpdate.EventId}]");
                                    logger.LogInformation($">> Session updated.");

                                    break;
                                }
                            case RealtimeServerUpdateInputAudioBufferSpeechStarted inputAudioBufferSpeechStartedUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", inputAudioBufferSpeechStartedUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {inputAudioBufferSpeechStartedUpdate.EventId}]");
                                    logger.LogInformation($">> Speech started at {inputAudioBufferSpeechStartedUpdate.AudioStartTime}.");

                                    break;
                                }
                            case RealtimeServerUpdateInputAudioBufferSpeechStopped inputAudioBufferSpeechStoppedUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", inputAudioBufferSpeechStoppedUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {inputAudioBufferSpeechStoppedUpdate.EventId}]");
                                    logger.LogInformation($">> Speech stopped at {inputAudioBufferSpeechStoppedUpdate.AudioEndTime}.");

                                    break;
                                }
                            case RealtimeServerUpdateConversationItemDone conversationItemDoneUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", conversationItemDoneUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {conversationItemDoneUpdate.EventId}]");
                                    logger.LogInformation($">> Conversation item done. Type: {conversationItemDoneUpdate.Item.GetType().Name}.");

                                    if (conversationItemDoneUpdate.Item is RealtimeMessageItem messageItem)
                                    {
                                        foreach (RealtimeMessageContentPart contentPart in messageItem.Content)
                                        {
                                            switch (contentPart)
                                            {
                                                case RealtimeInputTextMessageContentPart inputTextPart:
                                                    {
                                                        logger.LogInformation($"++ [{messageItem.Role.ToString().ToUpperInvariant()}]:");
                                                        logger.LogInformation(inputTextPart.Text);
                                                        break;
                                                    }
                                                case RealtimeOutputTextMessageContentPart outputTextPart:
                                                    {
                                                        logger.LogInformation($"++ [{messageItem.Role.ToString().ToUpperInvariant()}]:");
                                                        logger.LogInformation(outputTextPart.Text);
                                                        break;
                                                    }
                                            }
                                        }
                                    }

                                    break;
                                }
                            case RealtimeServerUpdateConversationItemInputAudioTranscriptionCompleted conversationItemInputAudioTranscriptionCompletedUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", conversationItemInputAudioTranscriptionCompletedUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {conversationItemInputAudioTranscriptionCompletedUpdate.EventId}]");
                                    logger.LogInformation($">> Conversation item input audio transcription completed.");


                                    logger.LogInformation($"++ [USER]: {conversationItemInputAudioTranscriptionCompletedUpdate.Transcript}");

                                    break;
                                }
                            case RealtimeServerUpdateResponseOutputAudioDelta responseOutputAudioDeltaUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", responseOutputAudioDeltaUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {responseOutputAudioDeltaUpdate.EventId}]");
                                    logger.LogInformation($">> Response output audio delta. Bytes: {responseOutputAudioDeltaUpdate.Delta.Length}.");

                                    outputAudioStream.Write(responseOutputAudioDeltaUpdate.Delta);

                                    break;
                                }
                            case RealtimeServerUpdateResponseOutputAudioDone responseOutputAudioDoneUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", responseOutputAudioDoneUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {responseOutputAudioDoneUpdate.EventId}]");
                                    logger.LogInformation($">> Response output audio done. Bytes: {outputAudioStream.Length}.");

                                    break;
                                }
                            case RealtimeServerUpdateResponseOutputAudioTranscriptDone responseOutputAudioTranscriptionDoneUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", responseOutputAudioTranscriptionDoneUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {responseOutputAudioTranscriptionDoneUpdate.EventId}]");
                                    logger.LogInformation($">> Response output audio transcription done.");


                                    logger.LogInformation($"++ [ASSISTANT]:");
                                    logger.LogInformation($"{responseOutputAudioTranscriptionDoneUpdate.Transcript}");

                                    break;
                                }
                            case RealtimeServerUpdateResponseDone responseDoneUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", responseDoneUpdate.EventId);

                                    logger.LogInformation($"[EVENT ID: {responseDoneUpdate.EventId}]");
                                    logger.LogInformation($">> Response done. Status: {responseDoneUpdate.Response.Status}.");

                                    bool hasToolCalls = false;

                                    List<RealtimeFunctionCallItem> functionCallItems = responseDoneUpdate.Response.OutputItems
                                        .OfType<RealtimeFunctionCallItem>()
                                        .ToList();

                                    foreach (RealtimeFunctionCallItem functionCallItem in functionCallItems)
                                    {
                                        hasToolCalls = true;

                                        logger.LogInformation($">> Calling {functionCallItem.FunctionName} function...");

                                        string output = string.Empty;
                                        if (functionCallItem.FunctionName == nameof(GetDateTime))
                                        {
                                            output = GetDateTime(location: "Boston, MA");
                                        }
                                        else if (functionCallItem.FunctionName == nameof(GetCurrentWeather))
                                        {

                                            output = GetCurrentWeather(location: "Boston, MA");
                                        }

                                        RealtimeItem functionCallOutputItem = RealtimeItem.CreateFunctionCallOutputItem(
                                            callId: functionCallItem.CallId,
                                            functionOutput: output);

                                        logger.LogInformation($">> Adding function call output item...");

                                        await sessionClient.AddItemAsync(functionCallOutputItem);
                                    }

                                    if (hasToolCalls)
                                    {
                                        // If we need the model to process the output of a tool call, we instruct
                                        // the server to create another responses.
                                        logger.LogInformation($">> Requesting follow up response...");

                                        await sessionClient.StartResponseAsync();
                                    }
                                    else
                                    {
                                        done = true;
                                        break;
                                    }

                                    break;
                                }
                            case RealtimeServerUpdateError errorUpdate:
                                {
                                    receivedUpdateActivity?.SetTag("update.event_id", errorUpdate.EventId);
                                    logger.LogInformation($"[EVENT ID: {errorUpdate.EventId}]");
                                    logger.LogInformation($"Error: {errorUpdate.Error.Message}");

                                    done = true;

                                    break;
                                }
                        }
                    }

                    if (done)
                    {
                        break;
                    }
                }
            }
        }
    }
}

public static class Program
{
    private const string ActivitySourceName = "realtime";
    private const string ActivitySourceVersion = "1.0.0";

    private static void ConfigureOtlpExporter(OtlpExporterOptions options, string endpoint, string headers)
    {
        options.Endpoint = new Uri(endpoint);
        options.Protocol = OtlpExportProtocol.HttpProtobuf;

        if (!string.IsNullOrWhiteSpace(headers))
        {
            options.Headers = headers;
        }
    }

    public static async Task Main(string[] args)
    {
        string otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? "http://localhost:4318";

        string newRelicLicenseKey = Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY") ?? string.Empty;
        string otlpHeaders = "api-key=" + newRelicLicenseKey;

        if (string.IsNullOrWhiteSpace(otlpHeaders) && !string.IsNullOrWhiteSpace(newRelicLicenseKey))
        {
            otlpHeaders = $"api-key={newRelicLicenseKey}";
        }

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
                options.Endpoint = new Uri(otlpEndpoint);
                options.Headers = otlpHeaders;
            })
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("OpenAI.*")
            .AddMeter(ActivitySourceName)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Headers = otlpHeaders;
            })
            .Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSimpleConsole(console =>
            {
                console.TimestampFormat = "HH:mm:ss ";
            });

            builder.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(resourceBuilder);
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.ParseStateValues = true;

                logging.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Headers = otlpHeaders;
                });
            });
        });

        // Get a logger instance
        var logger = loggerFactory.CreateLogger("RealtimeExamples");

        ActivitySource activitySource = new ActivitySource(ActivitySourceName, ActivitySourceVersion);

        using (var myMainActivity = activitySource.StartActivity("main", ActivityKind.Internal))
        {
            logger.LogInformation("Starting Realtime Examples...");
            logger.LogInformation("OTLP endpoint: {Endpoint}", otlpEndpoint);
            await OpenAI.Examples.RealtimeExamples.RunAsync(logger, activitySource);
        }

        tracerProvider.Dispose();
        meterProvider.Dispose();
        loggerFactory.Dispose();
    }
}

#pragma warning restore OPENAI002