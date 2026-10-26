using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Clippy.Services;

public sealed class OllamaTranslationService(HttpClient httpClient)
{
    public async Task<string> TranslateAsync(string source, string endpoint, string model,
        string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException(LocalizationManager.T("OllamaModelRequired"));
        var baseUri = endpoint.TrimEnd('/');
        var prompt = $"Translate the following text from {sourceLanguage} to {targetLanguage}. " +
                     "Return only the translation, preserving line breaks and formatting where possible.\n\n" + source;
        using var response = await httpClient.PostAsJsonAsync($"{baseUri}/api/generate",
            new GenerateRequest(model, prompt, false), cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken: cancellationToken);
        return result?.Response ?? throw new InvalidOperationException(LocalizationManager.T("OllamaEmptyResult"));
    }

    private sealed record GenerateRequest(string Model, string Prompt, bool Stream);
    private sealed class GenerateResponse
    {
        [JsonPropertyName("response")] public string Response { get; set; } = "";
    }
}
