using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

#pragma warning disable OPENAI001

NewRelic.Api.Agent.NewRelic.StartAgent();

[Transaction]
static async Task PirateChat()
{
    var tx = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;
    tx.AddCustomAttribute("aim", true);

    // The default transaction name at this point will be: WebTransaction/MVC/Home/Order

    // Set custom transaction name
    NewRelic.Api.Agent.NewRelic.SetTransactionName("Other", "PirateChat");

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
        new SystemChatMessage("Du bist ein hilfsbereiter Assistent, der auf Deutsch wie ein Pirat spricht."),
            new UserChatMessage("Hallo, kannst du mir helfen?"),
            new AssistantChatMessage("Arrr! Aber selbstverständlich, mein Freund! Was kann ich für dich tun?"),
            new UserChatMessage("Was ist der beste Weg, einen Papagei zu trainieren?"),
    ]);

    logger.LogInformation($"Model={completion.Model}");
    foreach (ChatMessageContentPart contentPart in completion.Content)
    {
        string message = contentPart.Text;
        logger.LogInformation($"Chat Role: {completion.Role}");
        logger.LogInformation($"Message: {message}");
    }
}

await PirateChat();

// Sleep for a bit to ensure all logs are flushed before the program exits.
Thread.Sleep(5000);