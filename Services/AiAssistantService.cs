using System.Runtime.CompilerServices;
using GameLauncher.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GameLauncher.Services;

public enum AiProvider { Groq, OpenAI }

/// <summary>
/// Assistente IA gamer usando Semantic Kernel.
/// Suporta Groq (grátis) e OpenAI (pago). Groq não suporta visão.
/// </summary>
public class AiAssistantService : IDisposable
{
    private readonly IChatCompletionService _chat;
    private readonly ChatHistory _history;
    private readonly AiProvider _provider;

    // Endpoint compatível com OpenAI usado pelo Groq
    private static readonly Uri GroqEndpoint = new("https://api.groq.com/openai/v1");

    public AiAssistantService(AiProvider provider, string apiKey, string? currentGameName = null)
    {
        _provider = provider;

        var builder = Kernel.CreateBuilder();

        if (provider == AiProvider.Groq)
        {
            // Groq é compatível com a API OpenAI; usa llama-3.3-70b gratuito
            builder.AddOpenAIChatCompletion(
                modelId: "llama-3.3-70b-versatile",
                apiKey: apiKey,
                endpoint: GroqEndpoint);
        }
        else
        {
            builder.AddOpenAIChatCompletion("gpt-4o", apiKey);
        }

        var kernel = builder.Build();
        _chat = kernel.GetRequiredService<IChatCompletionService>();
        _history = new ChatHistory();
        _history.AddSystemMessage(BuildSystemPrompt(currentGameName));
    }

    /// <summary>
    /// Envia uma mensagem (com screenshot opcional) e retorna a resposta em streaming.
    /// Análise de screenshot só está disponível com OpenAI (GPT-4o).
    /// </summary>
    public async IAsyncEnumerable<string> SendAsync(
        string userMessage,
        string? imageBase64 = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (imageBase64 is not null && _provider == AiProvider.OpenAI)
        {
            var imageBytes = Convert.FromBase64String(imageBase64);
            var content = new ChatMessageContentItemCollection
            {
                new TextContent(userMessage),
                new ImageContent(imageBytes, "image/png")
            };
            _history.AddUserMessage(content);
        }
        else
        {
            if (imageBase64 is not null)
                _history.AddUserMessage($"[Screenshot enviado — análise de imagem não disponível no modo gratuito]\n{userMessage}");
            else
                _history.AddUserMessage(userMessage);
        }

        var fullResponse = new System.Text.StringBuilder();

        await foreach (var chunk in _chat.GetStreamingChatMessageContentsAsync(
            _history, cancellationToken: cancellationToken))
        {
            var token = chunk.Content ?? string.Empty;
            if (!string.IsNullOrEmpty(token))
            {
                fullResponse.Append(token);
                yield return token;
            }
        }

        _history.AddAssistantMessage(fullResponse.ToString());
    }

    private static string BuildSystemPrompt(string? gameName)
    {
        var gameContext = gameName is not null
            ? $"O jogador está jogando **{gameName}** agora."
            : "O jogador não está jogando nenhum jogo no momento.";

        return $"""
            Você é o **GLauncher AI**, um assistente especialista em jogos integrado ao GLauncher.
            {gameContext}

            Suas diretrizes:
            - Responda sempre em português do Brasil, de forma amigável e entusiasmada.
            - Dê dicas táticas, estratégicas e de progressão sem revelar spoilers importantes, a menos que o usuário peça explicitamente.
            - Se o usuário enviar um screenshot, analise a situação do jogo e ofereça conselhos contextuais.
            - Seja conciso mas útil. Prefira listas quando listar dicas.
            - Se não souber algo sobre um jogo específico, admita honestamente.
            - Mantenha o histórico da conversa para dar respostas contextuais.
            """;
    }

    public void Dispose() { }
}
