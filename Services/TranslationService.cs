using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace GameLauncher.Services;

public static class TranslationService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // Dicionário de gêneros EN → PT-BR
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

    /// <summary>
    /// Traduz os gêneros usando o dicionário local. Gêneros não mapeados são mantidos como estão.
    /// </summary>
    public static string TranslateGenres(string genres)
    {
        if (string.IsNullOrWhiteSpace(genres) || genres == "—")
            return genres;

        var parts = genres.Split(',', StringSplitOptions.TrimEntries);
        var translated = parts.Select(g => GenreMap.TryGetValue(g, out var pt) ? pt : g);
        return string.Join(", ", translated);
    }

    /// <summary>
    /// Traduz texto de EN para PT-BR usando a API gratuita MyMemory.
    /// Retorna o texto original em caso de falha.
    /// </summary>
    public static async Task<string> TranslateToPortugueseAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

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

            // MyMemory retorna em CAPS quando excede cota — detecta e retorna original
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
