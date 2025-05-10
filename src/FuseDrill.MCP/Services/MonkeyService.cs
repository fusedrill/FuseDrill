using System.Net.Http.Json;
using FuseDrill.MCP.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace FuseDrill.MCP.Services;

public class MonkeyService
{
    private readonly HttpClient httpClient;
    public MonkeyService(IHttpClientFactory factory)
    {
        httpClient = factory.CreateClient();
    }

    private List<Monkey> monkeyList = [];

    public async Task<List<Monkey>> GetMonkeys()
    {
        if (monkeyList.Count > 0)
            return monkeyList;

        var response = await httpClient.GetAsync("https://www.montemagno.com/monkeys.json");
        if (response.IsSuccessStatusCode)
        {
            monkeyList = await response.Content.ReadFromJsonAsync(MonkeyContext.Default.ListMonkey) ?? [];
        }

        return monkeyList;
    }

    public async Task<Monkey?> GetMonkey(string name)
    {
        var monkeys = await GetMonkeys();
        return monkeys.FirstOrDefault(m => m.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
    }
}
