#:package OpenAI@2.9.1
#:package Microsoft.Extensions.Logging.Console@10.0.5
#:package NewRelic.Agent@10.50.0

using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Extensions.Logging;


#pragma warning disable OPENAI001

const string deploymentName = "gpt-5-mini";
string endpoint = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_ENDPOINT") ?? "https://api.openai.com/v1";
string apiKey = Environment.GetEnvironmentVariable("MSFT_FOUNDRY_API_KEY") ?? "YOUR_API_KEY_HERE";

using ILoggerFactory factory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});
ILogger logger = factory.CreateLogger("Program");
logger.LogInformation("Hello World! Logging is {Description}.", "fun");

ChatClient client = new(
    credential: new ApiKeyCredential(apiKey),
    model: deploymentName,
    options: new OpenAIClientOptions()
    {
        Endpoint = new($"{endpoint}"),
    });

ChatCompletion completion = await client.CompleteChatAsync(
[
    new SystemChatMessage("You are a helpful assistant that talks like a pirate."),
            new UserChatMessage("Hi, can you help me?"),
            new AssistantChatMessage("Arrr! Of course, me hearty! What can I do for ye?"),
            new UserChatMessage("What's the best way to train a parrot?"),
        ]);

logger.LogInformation($"Model={completion.Model}");
foreach (ChatMessageContentPart contentPart in completion.Content)
{
    string message = contentPart.Text;
    logger.LogInformation($"Chat Role: {completion.Role}");
    logger.LogInformation($"Message: {message}");
}

Thread.Sleep(15000); // Sleep for a bit to ensure all logs are flushed before the program exits.