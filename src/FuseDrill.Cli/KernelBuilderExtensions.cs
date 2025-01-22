using Microsoft.SemanticKernel;

public static class KernelBuilderExtensions
{
    public static IKernelBuilder AddLMStudioChatCompletionLocal(this IKernelBuilder builder)
    {
        var client = new HttpClient(new MyHttpMessageHandler());
        client.Timeout = TimeSpan.FromMinutes(5);
        // LMStudio by default will ignore the local-api-key and local-model parameters.
        builder.AddOpenAIChatCompletion("local-model", "lm-studio", httpClient: client);
        return builder;
    }

    public static IKernelBuilder AddGeminiChatCompletion(this IKernelBuilder builder)
    {
        //var client = new HttpClient(new MyHttpMessageHandlerGemini());
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash-latest", Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
#pragma warning restore SKEXP0070 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return builder;
    }

    public static IKernelBuilder AddGithubChatCompletion(this IKernelBuilder builder)
    {
        var endpoint = new Uri("https://models.inference.ai.azure.com").ToString();
        var credential = System.Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var model = "gpt-4o-mini";

        builder.AddAzureOpenAIChatCompletion("gpt-4o-mini", endpoint, credential, modelId: model);
        return builder;
    }

}

