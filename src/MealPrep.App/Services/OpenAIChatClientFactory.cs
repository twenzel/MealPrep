using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MealPrep.App.Services;

public interface IOpenAIChatClientFactory
{
    IChatClient? GetClient(string model);
}

public sealed class OpenAIChatClientFactory(OpenAIProviderOptions options)
    : IOpenAIChatClientFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IChatClient> clients =
        new(StringComparer.OrdinalIgnoreCase);

    public IChatClient? GetClient(string model)
    {
        if (!options.IsConfigured || string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        return clients.GetOrAdd(model.Trim(), CreateClient);
    }

    private IChatClient CreateClient(string model)
    {
#pragma warning disable OPENAI001
        return new OpenAIClient(options.ApiKey!)
            .GetResponsesClient()
            .AsIChatClient(model);
#pragma warning restore OPENAI001
    }

    public void Dispose()
    {
        foreach (var client in clients.Values)
        {
            (client as IDisposable)?.Dispose();
        }

        clients.Clear();
    }
}
