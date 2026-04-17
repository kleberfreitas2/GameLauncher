namespace GameLauncher.Models;

public enum TrophyRarity
{
    Bronze,
    Silver,
    Gold,
    Platinum
}

public enum TrophyId
{
    // Bronze
    Welcome,
    FirstGame,
    Explorer,
    CustomAvatar,
    Decorator,
    Configurator,
    SocialDiscord,
    XboxLive,
    SteamGamer,
    EpicGamer,

    // Silver
    OneHourLauncher,
    Collector10,
    Favorited3,
    AiAssistant,
    Marathoner3h,
    Organizer,
    BigPictureBeginner,
    EasterEggHunter,

    // Gold
    Veteran5h,
    Library25,
    RealGamer10h,
    AiExpert,
    AllInOne,
    BigPicturePro,

    // Platinum
    Legendary24h,
    Completionist50,
    GlauncherMaster
}

public class Trophy
{
    public TrophyId Id           { get; init; }
    public TrophyRarity Rarity   { get; init; }
    public string Name           { get; init; } = string.Empty;
    public string Description    { get; init; } = string.Empty;
    public string Icon           { get; init; } = "🏆";
    public int Gamerscore        { get; init; }

    public bool IsUnlocked       { get; set; }
    public DateTime? UnlockedAt  { get; set; }

    public string RarityLabel => Rarity switch
    {
        TrophyRarity.Bronze   => "Bronze",
        TrophyRarity.Silver   => "Prata",
        TrophyRarity.Gold     => "Ouro",
        TrophyRarity.Platinum => "Platina",
        _                     => "?"
    };

    public string RarityColor => Rarity switch
    {
        TrophyRarity.Bronze   => "#CD7F32",
        TrophyRarity.Silver   => "#C0C0C0",
        TrophyRarity.Gold     => "#FFD700",
        TrophyRarity.Platinum => "#00CFFF",
        _                     => "#FFFFFF"
    };

    public string RarityGlow => Rarity switch
    {
        TrophyRarity.Bronze   => "#7A3F00",
        TrophyRarity.Silver   => "#888888",
        TrophyRarity.Gold     => "#AA8800",
        TrophyRarity.Platinum => "#0088CC",
        _                     => "#555555"
    };

    public string UnlockedAtText => UnlockedAt.HasValue
        ? UnlockedAt.Value.ToString("dd/MM/yyyy HH:mm")
        : string.Empty;

    public static IReadOnlyList<Trophy> CreateAll() =>
    [
        // ── Bronze ───────────────────────────────────────────────────────────
        new Trophy { Id = TrophyId.Welcome,         Rarity = TrophyRarity.Bronze, Gamerscore = 10,
            Name = "Bem-vindo, Gamer!",     Icon = "🎮",
            Description = "Abriu o GLauncher pela primeira vez. A jornada começa aqui!" },

        new Trophy { Id = TrophyId.FirstGame,       Rarity = TrophyRarity.Bronze, Gamerscore = 10,
            Name = "Primeiro Jogo!",        Icon = "🕹️",
            Description = "Adicionou seu primeiro jogo à biblioteca. Apenas o começo..." },

        new Trophy { Id = TrophyId.Explorer,        Rarity = TrophyRarity.Bronze, Gamerscore = 10,
            Name = "Explorador",            Icon = "🔍",
            Description = "Abriu detalhes de 5 jogos diferentes. Curioso(a)!" },

        new Trophy { Id = TrophyId.CustomAvatar,    Rarity = TrophyRarity.Bronze, Gamerscore = 10,
            Name = "Identidade Gamer",      Icon = "🪪",
            Description = "Personalizou seu avatar. Agora é oficial!" },

        new Trophy { Id = TrophyId.Decorator,       Rarity = TrophyRarity.Bronze, Gamerscore = 10,
            Name = "Decorador",             Icon = "🖼️",
            Description = "Mudou o plano de fundo do GLauncher. Bom gosto!" },

        new Trophy { Id = TrophyId.Configurator,    Rarity = TrophyRarity.Bronze, Gamerscore = 10,
            Name = "Configurador",          Icon = "⚙️",
            Description = "Abriu as configurações. Sempre bom saber o que tem lá!" },

        new Trophy { Id = TrophyId.SocialDiscord,   Rarity = TrophyRarity.Bronze, Gamerscore = 15,
            Name = "Social Gamer",          Icon = "💬",
            Description = "Conectou o Discord. Ninguém joga sozinho!" },

        new Trophy { Id = TrophyId.XboxLive,        Rarity = TrophyRarity.Bronze, Gamerscore = 15,
            Name = "Xbox Live!",            Icon = "🟢",
            Description = "Conectou ao Xbox Live. Achievement unlocked!" },

        new Trophy { Id = TrophyId.SteamGamer,      Rarity = TrophyRarity.Bronze, Gamerscore = 15,
            Name = "Steam Gamer",           Icon = "🚂",
            Description = "Conectou ao Steam. A maior loja do PC!" },

        new Trophy { Id = TrophyId.EpicGamer,       Rarity = TrophyRarity.Bronze, Gamerscore = 15,
            Name = "Épico!",                Icon = "⚡",
            Description = "Conectou à Epic Games. Free games toda semana!" },

        // ── Prata ───────────────────────────────────────────────────────────
        new Trophy { Id = TrophyId.OneHourLauncher, Rarity = TrophyRarity.Silver, Gamerscore = 30,
            Name = "Uma Hora Lendária",     Icon = "⏱️",
            Description = "Ficou 1 hora usando o GLauncher. Você gosta mesmo!" },

        new Trophy { Id = TrophyId.Collector10,     Rarity = TrophyRarity.Silver, Gamerscore = 25,
            Name = "Colecionador",          Icon = "📚",
            Description = "Tem 10 jogos na biblioteca. Está crescendo!" },

        new Trophy { Id = TrophyId.Favorited3,      Rarity = TrophyRarity.Silver, Gamerscore = 20,
            Name = "Favoritador",           Icon = "⭐",
            Description = "Marcou 3 jogos como favorito. Os clássicos preferidos!" },

        new Trophy { Id = TrophyId.AiAssistant,     Rarity = TrophyRarity.Silver, Gamerscore = 20,
            Name = "Assistente IA",         Icon = "🤖",
            Description = "Usou a GLauncher IA pela primeira vez. O futuro chegou!" },

        new Trophy { Id = TrophyId.Marathoner3h,    Rarity = TrophyRarity.Silver, Gamerscore = 30,
            Name = "Maratonista",           Icon = "🏃",
            Description = "Jogou 3 horas via GLauncher. Sessão épica!" },

        new Trophy { Id = TrophyId.Organizer,       Rarity = TrophyRarity.Silver, Gamerscore = 20,
            Name = "Organizador",           Icon = "✏️",
            Description = "Renomeou um jogo. Perfeicionismo é uma virtude!" },

        new Trophy { Id = TrophyId.BigPictureBeginner, Rarity = TrophyRarity.Silver, Gamerscore = 25,
            Name = "TV Gamer",              Icon = "📺",
            Description = "Ativou o Modo Big Picture pela primeira vez. Na TV fica melhor!" },

        new Trophy { Id = TrophyId.EasterEggHunter, Rarity = TrophyRarity.Silver, Gamerscore = 30,
            Name = "Caçador de Easter Eggs", Icon = "🥚",
            Description = "Encontrou o Easter Egg escondido no logo. Só os lendários chegam aqui!" },

        // ── Ouro ─────────────────────────────────────────────────────────────
        new Trophy { Id = TrophyId.Veteran5h,       Rarity = TrophyRarity.Gold, Gamerscore = 50,
            Name = "Veterano",              Icon = "🎖️",
            Description = "Ficou 5 horas usando o GLauncher. Fã número 1!" },

        new Trophy { Id = TrophyId.Library25,       Rarity = TrophyRarity.Gold, Gamerscore = 60,
            Name = "Biblioteca Épica",      Icon = "🏛️",
            Description = "Tem 25 jogos na biblioteca. Impressionante coleção!" },

        new Trophy { Id = TrophyId.RealGamer10h,    Rarity = TrophyRarity.Gold, Gamerscore = 70,
            Name = "Gamer de Verdade",      Icon = "🔥",
            Description = "Jogou 10 horas via GLauncher. Dedicação total!" },

        new Trophy { Id = TrophyId.AiExpert,        Rarity = TrophyRarity.Gold, Gamerscore = 50,
            Name = "Expert em IA",          Icon = "🧠",
            Description = "Usou a IA do GLauncher 10 vezes. O assistente perfeito!" },

        new Trophy { Id = TrophyId.AllInOne,        Rarity = TrophyRarity.Gold, Gamerscore = 50,
            Name = "All-in-One",            Icon = "🌐",
            Description = "Conectou Xbox Live, Steam e Epic Games. Centralizado!" },

        new Trophy { Id = TrophyId.BigPicturePro,   Rarity = TrophyRarity.Gold, Gamerscore = 50,
            Name = "Big Picture Pro",       Icon = "🎬",
            Description = "Usou o Modo Big Picture 5 vezes. A TV nunca foi tão gamer!" },

        // ── Platina ──────────────────────────────────────────────────────────
        new Trophy { Id = TrophyId.Legendary24h,    Rarity = TrophyRarity.Platinum, Gamerscore = 100,
            Name = "Lendário",              Icon = "👑",
            Description = "Ficou 24 horas usando o GLauncher. Uma lenda viva!" },

        new Trophy { Id = TrophyId.Completionist50, Rarity = TrophyRarity.Platinum, Gamerscore = 100,
            Name = "Completista",           Icon = "💎",
            Description = "Tem 50 jogos na biblioteca. A maior coleção do Brasil!" },

        new Trophy { Id = TrophyId.GlauncherMaster, Rarity = TrophyRarity.Platinum, Gamerscore = 150,
            Name = "GLauncher Master",      Icon = "🏆",
            Description = "Desbloqueou todos os outros troféus. Você é o(a) mestre absoluto(a)!" },
    ];
}
