using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace GameLauncher.Services;

public static class TranslationService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly Dictionary<string, string> GenreMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"]                = "Ação",
        ["Adventure"]             = "Aventura",
        ["Arcade"]                = "Arcade",
        ["Battle Royale"]         = "Battle Royale",
        ["Board Game"]            = "Jogo de Tabuleiro",
        ["Card Game"]             = "Jogo de Cartas",
        ["Casual"]                = "Casual",
        ["City Builder"]          = "Construção de Cidades",
        ["Co-op"]                 = "Cooperativo",
        ["Comedy"]                = "Comédia",
        ["Dating Simulator"]      = "Simulador de Encontros",
        ["Driving"]               = "Corrida",
        ["Educational"]           = "Educativo",
        ["Exploration"]           = "Exploração",
        ["Fantasy"]               = "Fantasia",
        ["Fighting"]              = "Luta",
        ["First Person Shooter"]  = "Tiro em Primeira Pessoa",
        ["FPS"]                   = "FPS",
        ["Hack and Slash"]        = "Hack and Slash",
        ["Horror"]                = "Terror",
        ["Indie"]                 = "Indie",
        ["JRPG"]                  = "JRPG",
        ["MMORPG"]                = "MMORPG",
        ["MOBA"]                  = "MOBA",
        ["Music"]                 = "Música",
        ["Mystery"]               = "Mistério",
        ["Open World"]            = "Mundo Aberto",
        ["Party"]                 = "Festa",
        ["Pinball"]               = "Pinball",
        ["Platform"]              = "Plataforma",
        ["Platformer"]            = "Plataforma",
        ["Point-and-click"]       = "Apontar e Clicar",
        ["Puzzle"]                = "Puzzle",
        ["Quiz/Trivia"]           = "Quiz/Trivia",
        ["Racing"]                = "Corrida",
        ["Real Time Strategy"]    = "Estratégia em Tempo Real",
        ["Real-time strategy (RTS)"] = "Estratégia em Tempo Real (RTS)",
        ["Rhythm"]                = "Ritmo",
        ["Role-playing (RPG)"]    = "RPG",
        ["RPG"]                   = "RPG",
        ["RTS"]                   = "RTS",
        ["Sandbox"]               = "Sandbox",
        ["Sci-Fi"]                = "Ficção Científica",
        ["Shooter"]               = "Tiro",
        ["Simulation"]            = "Simulação",
        ["Simulator"]             = "Simulador",
        ["Sport"]                 = "Esporte",
        ["Sports"]                = "Esportes",
        ["Stealth"]               = "Furtividade",
        ["Strategy"]              = "Estratégia",
        ["Survival"]              = "Sobrevivência",
        ["Survival Horror"]       = "Terror e Sobrevivência",
        ["Tactical"]              = "Tático",
        ["Third Person Shooter"]  = "Tiro em Terceira Pessoa",
        ["TPS"]                   = "TPS",
        ["Turn-based strategy (TBS)"] = "Estratégia por Turnos (TBS)",
        ["Turn-based"]            = "Por Turnos",
        ["Visual Novel"]          = "Visual Novel",
        ["Wrestling"]             = "Luta Livre",
    };

    public static string TranslateGenres(string genres)
    {
        if (string.IsNullOrWhiteSpace(genres) || genres == "—")
            return genres;

        var parts = genres.Split(',', StringSplitOptions.TrimEntries);
        var translated = parts.Select(g => GenreMap.TryGetValue(g, out var pt) ? pt : g);
        return string.Join(", ", translated);
    }

    public static async Task<string> TranslateToPortugueseAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        const int maxChunkLength = 450;

        if (text.Length <= maxChunkLength)
            return await TranslateChunkAsync(text);

        var chunks = SplitIntoChunks(text, maxChunkLength);
        var translatedParts = new List<string>();

        foreach (var chunk in chunks)
        {
            var translated = await TranslateChunkAsync(chunk);
            translatedParts.Add(translated);
            await Task.Delay(300); // Respeita rate limit da API
        }

        return string.Join(" ", translatedParts);
    }

    private static List<string> SplitIntoChunks(string text, int maxLength)
    {
        var chunks = new List<string>();
        var sentences = text.Split([". ", "! ", "? "], StringSplitOptions.None);

        var current = "";
        for (int i = 0; i < sentences.Length; i++)
        {
            var sentence = sentences[i];
            var separator = i < sentences.Length - 1 ? ". " : "";
            var candidate = string.IsNullOrEmpty(current)
                ? sentence + separator
                : current + sentence + separator;

            if (candidate.Length > maxLength && !string.IsNullOrEmpty(current))
            {
                chunks.Add(current.TrimEnd());
                current = sentence + separator;
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
            chunks.Add(current.TrimEnd());

        return chunks;
    }

    private static async Task<string> TranslateChunkAsync(string text)
    {
        try
        {
            var encoded = HttpUtility.UrlEncode(text);
            var url = $"https://api.mymemory.translated.net/get?q={encoded}&langpair=en|pt-br";

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var translated = doc.RootElement
                .GetProperty("responseData")
                .GetProperty("translatedText")
                .GetString();

            if (translated is not null &&
                !string.Equals(translated, text, StringComparison.Ordinal) &&
                translated != translated.ToUpperInvariant())
            {
                return translated;
            }

            return text;
        }
        catch
        {
            return text;
        }
    }
}
