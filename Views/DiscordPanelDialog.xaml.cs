using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class DiscordPanelDialog : Window
{
    private readonly DiscordRpcService? _rpc;
    private readonly DiscordService? _discordService;
    private readonly string _webhookUrl;
    private readonly List<string> _quickMessages;

    private int _activeTab;
    private readonly Border[] _tabs;
    private readonly Grid[] _panels;

    public bool LogoutRequested { get; private set; }

    private List<DiscordGuild> _guilds = [];
    private List<DiscordVoiceChannel> _channels = [];
    private readonly ObservableCollection<DiscordVoiceUser> _voiceUsers = [];
    private readonly ObservableCollection<NotificationItem> _notifications = [];
    private readonly ObservableCollection<DiscordDmChannel> _dmChannels = [];

    public DiscordPanelDialog(DiscordRpcService? rpc, DiscordService? discordService = null)
    {
        InitializeComponent();

        _rpc = rpc;
        _discordService = discordService;
        _webhookUrl = SettingsService.Current.DiscordWebhookUrl;
        _quickMessages = new List<string>(SettingsService.Current.DiscordQuickMessages);

        _tabs = [TabVoice, TabMessages, TabNotifications];
        _panels = [VoicePanel, MessagesPanel, NotificationsPanel];

        VoiceUsersList.ItemsSource = _voiceUsers;
        NotificationsList.ItemsSource = _notifications;
        DmChannelsList.ItemsSource = _dmChannels;
        QuickMessagesList.ItemsSource = _quickMessages;
        WebhookUrlBox.Text = _webhookUrl;

        if (_quickMessages.Count > 0)
            QuickMessagesList.SelectedIndex = 0;

        if (_rpc is not null)
        {
            _rpc.VoiceUserJoined += OnVoiceUserJoined;
            _rpc.VoiceUserLeft += OnVoiceUserLeft;
            _rpc.NotificationReceived += OnNotification;
            _rpc.VoiceChannelJoined += OnVoiceChannelJoined;
            _rpc.VoiceChannelLeft += OnVoiceChannelLeft;
            _rpc.MuteChanged += OnMuteChanged;
            _rpc.DeafChanged += OnDeafChanged;
        }

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadRpcData();
        _ = LoadDmChannelsAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_rpc is not null)
        {
            _rpc.VoiceUserJoined -= OnVoiceUserJoined;
            _rpc.VoiceUserLeft -= OnVoiceUserLeft;
            _rpc.NotificationReceived -= OnNotification;
            _rpc.VoiceChannelJoined -= OnVoiceChannelJoined;
            _rpc.VoiceChannelLeft -= OnVoiceChannelLeft;
            _rpc.MuteChanged -= OnMuteChanged;
            _rpc.DeafChanged -= OnDeafChanged;
        }
    }

    private void LoadRpcData()
    {
        if (_rpc is null || !_rpc.IsConnected)
        {
            CurrentChannelText.Text = "RPC não conectado";
            return;
        }

        _guilds = _rpc.GetGuilds();
        GuildCombo.ItemsSource = _guilds;
        if (_guilds.Count > 0)
            GuildCombo.SelectedIndex = 0;

        var (mute, deaf) = _rpc.GetVoiceSettings();
        UpdateMuteUI(mute);
        UpdateDeafUI(deaf);

        if (_rpc.CurrentVoiceChannelId is not null)
        {
            CurrentChannelText.Text = _rpc.CurrentVoiceChannelName ?? _rpc.CurrentVoiceChannelId;
            RefreshVoiceUsers(_rpc.CurrentVoiceChannelId);
        }
    }

    private void GuildCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GuildCombo.SelectedItem is not DiscordGuild guild || _rpc is null) return;

        _channels = _rpc.GetVoiceChannels(guild.Id);
        ChannelList.ItemsSource = _channels;
    }

    private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelList.SelectedItem is DiscordVoiceChannel ch)
            RefreshVoiceUsers(ch.Id);
    }

    private void RefreshVoiceUsers(string channelId)
    {
        if (_rpc is null) return;
        var users = _rpc.GetChannelVoiceUsers(channelId);
        _voiceUsers.Clear();
        foreach (var u in users)
            _voiceUsers.Add(u);

        NoUsersText.Visibility = _voiceUsers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadDmChannelsAsync()
    {
        if (_discordService is null || !_discordService.IsLoggedIn)
        {
            DmStatusText.Text = "Faça login no Discord para ver suas DMs";
            DmStatusText.Visibility = Visibility.Visible;
            DmLoadingBar.Visibility = Visibility.Collapsed;
            return;
        }

        DmLoadingBar.Visibility = Visibility.Visible;
        DmStatusText.Text = "";
        DmStatusText.Visibility = Visibility.Collapsed;

        var (channels, error) = await _discordService.GetDmChannelsAsync();

        DmLoadingBar.Visibility = Visibility.Collapsed;

        _dmChannels.Clear();
        foreach (var ch in channels)
            _dmChannels.Add(ch);

        if (_dmChannels.Count == 0)
        {
            DmStatusText.Text = error switch
            {
                "dm_not_available" =>
                    "O Discord não permite leitura de DMs\n" +
                    "para aplicações não verificadas.\n\n" +
                    "Use a seção 'Envio Rápido' abaixo\n" +
                    "para enviar mensagens via Webhook.",
                not null => $"Erro ao carregar DMs: {error}",
                _ => "Nenhuma DM encontrada."
            };
            DmStatusText.Visibility = Visibility.Visible;
        }
        else
        {
            DmStatusText.Visibility = Visibility.Collapsed;
        }
    }

    // Button handlers
    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        LogoutRequested = true;
        DialogResult = true;
    }

    private void BtnJoin_Click(object sender, RoutedEventArgs e) => JoinSelectedChannel();
    private void BtnLeave_Click(object sender, RoutedEventArgs e) => LeaveChannel();
    private void BtnMute_Click(object sender, RoutedEventArgs e) => ToggleMute();
    private void BtnDeafen_Click(object sender, RoutedEventArgs e) => ToggleDeafen();

    private async void BtnSendMsg_Click(object sender, RoutedEventArgs e) => await SendSelectedMessage();

    private void JoinSelectedChannel()
    {
        if (_rpc is null || ChannelList.SelectedItem is not DiscordVoiceChannel ch) return;
        _rpc.JoinVoiceChannel(ch.Id);
    }

    private void LeaveChannel()
    {
        _rpc?.LeaveVoiceChannel();
        CurrentChannelText.Text = "Nenhum";
        _voiceUsers.Clear();
        NoUsersText.Visibility = Visibility.Visible;
    }

    private void ToggleMute()
    {
        if (_rpc is null) return;
        _rpc.ToggleMute();
        UpdateMuteUI(_rpc.IsMuted);
    }

    private void ToggleDeafen()
    {
        if (_rpc is null) return;
        _rpc.ToggleDeafen();
        UpdateDeafUI(_rpc.IsDeafened);
    }

    private async Task SendSelectedMessage()
    {
        var url = WebhookUrlBox.Text?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            MsgStatusText.Text = "Configure o Webhook URL primeiro!";
            MsgStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"));
            return;
        }

        var msg = QuickMessagesList.SelectedItem as string;
        if (string.IsNullOrEmpty(msg))
        {
            MsgStatusText.Text = "Selecione uma mensagem!";
            MsgStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"));
            return;
        }

        MsgStatusText.Text = "Enviando...";
        MsgStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD740"));

        var success = await DiscordWebhookService.SendMessageAsync(url, msg, "GLauncher");
        MsgStatusText.Text = success ? "✅ Mensagem enviada!" : "❌ Falha ao enviar";
        MsgStatusText.Foreground = success
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"));
    }

    private void WebhookUrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SettingsService.Current.DiscordWebhookUrl = WebhookUrlBox.Text?.Trim() ?? "";
        SettingsService.Save();
    }

    // UI updates
    private void UpdateMuteUI(bool muted)
    {
        var muteIcon = (MaterialDesignThemes.Wpf.PackIcon)FindName("MuteIcon");
        var muteLabel = (TextBlock)FindName("MuteLabel");
        if (muteIcon is null || muteLabel is null) return;

        muteIcon.Kind = muted
            ? MaterialDesignThemes.Wpf.PackIconKind.MicrophoneOff
            : MaterialDesignThemes.Wpf.PackIconKind.Microphone;
        muteIcon.Foreground = muted
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676"));
        muteLabel.Text = muted ? "[Y] UNMUTE" : "[Y] MUTE";
    }

    private void UpdateDeafUI(bool deafened)
    {
        var deafIcon = (MaterialDesignThemes.Wpf.PackIcon)FindName("DeafIcon");
        var deafLabel = (TextBlock)FindName("DeafLabel");
        if (deafIcon is null || deafLabel is null) return;

        deafIcon.Kind = deafened
            ? MaterialDesignThemes.Wpf.PackIconKind.HeadphonesOff
            : MaterialDesignThemes.Wpf.PackIconKind.Headphones;
        deafIcon.Foreground = deafened
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676"));
        deafLabel.Text = deafened ? "[X] ESCUTAR" : "[X] SILENCIAR";
    }

    // RPC events
    private void OnVoiceUserJoined(DiscordVoiceUser user)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_voiceUsers.All(u => u.UserId != user.UserId))
                _voiceUsers.Add(user);
            NoUsersText.Visibility = Visibility.Collapsed;
        });
    }

    private void OnVoiceUserLeft(DiscordVoiceUser user)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var existing = _voiceUsers.FirstOrDefault(u => u.UserId == user.UserId);
            if (existing is not null) _voiceUsers.Remove(existing);
            NoUsersText.Visibility = _voiceUsers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void OnNotification(string title, string body)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _notifications.Insert(0, new NotificationItem
            {
                Title = title,
                Body = body,
                Time = DateTime.Now
            });

            if (_notifications.Count > 50)
                _notifications.RemoveAt(_notifications.Count - 1);

            NoNotificationsText.Visibility = Visibility.Collapsed;
        });
    }

    private void OnVoiceChannelJoined(string channelName)
    {
        Dispatcher.BeginInvoke(() => CurrentChannelText.Text = channelName);
    }

    private void OnVoiceChannelLeft()
    {
        Dispatcher.BeginInvoke(() =>
        {
            CurrentChannelText.Text = "Nenhum";
            _voiceUsers.Clear();
            NoUsersText.Visibility = Visibility.Visible;
        });
    }

    private void OnMuteChanged(bool muted)
    {
        Dispatcher.BeginInvoke(() => UpdateMuteUI(muted));
    }

    private void OnDeafChanged(bool deafened)
    {
        Dispatcher.BeginInvoke(() => UpdateDeafUI(deafened));
    }

    // Tab switching
    private void Tab_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && int.TryParse(border.Tag?.ToString(), out int idx))
            SwitchTab(idx);
    }

    private void SwitchTab(int index)
    {
        if (index < 0 || index >= _tabs.Length) return;
        _activeTab = index;

        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].Background = i == index
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5865F2"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22FFFFFF"));

            var panel = _tabs[i].Child as StackPanel;
            if (panel?.Children.Count >= 2 && panel.Children[1] is TextBlock tb)
            {
                tb.Foreground = i == index
                    ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8899AA"));
            }
            if (panel?.Children.Count >= 1 && panel.Children[0] is MaterialDesignThemes.Wpf.PackIcon icon)
            {
                icon.Foreground = i == index
                    ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8899AA"));
            }

            _panels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Gamepad navigation
    public void HandleGamepadInput(GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.B:
            case GamepadButton.Back:
                DialogResult = false;
                break;

            case GamepadButton.LeftShoulder:
                SwitchTab((_activeTab - 1 + _tabs.Length) % _tabs.Length);
                break;

            case GamepadButton.RightShoulder:
                SwitchTab((_activeTab + 1) % _tabs.Length);
                break;

            case GamepadButton.Y:
                ToggleMute();
                break;

            case GamepadButton.X:
                ToggleDeafen();
                break;

            case GamepadButton.A:
                HandleAction();
                break;

            case GamepadButton.DPadUp:
                NavigateList(-1);
                break;

            case GamepadButton.DPadDown:
                NavigateList(1);
                break;
        }
    }

    private void HandleAction()
    {
        switch (_activeTab)
        {
            case 0: // Voice
                JoinSelectedChannel();
                break;
            case 1: // Messages
                _ = SendSelectedMessage();
                break;
        }
    }

    private void NavigateList(int delta)
    {
        ListBox? list = _activeTab switch
        {
            0 => ChannelList,
            1 => DmChannelsList.Items.Count > 0 ? DmChannelsList : QuickMessagesList,
            _ => null
        };

        if (list is null || list.Items.Count == 0) return;

        int idx = list.SelectedIndex + delta;
        if (idx < 0) idx = list.Items.Count - 1;
        if (idx >= list.Items.Count) idx = 0;
        list.SelectedIndex = idx;
        list.ScrollIntoView(list.SelectedItem);
    }

    public class NotificationItem
    {
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime Time { get; set; }
        public string TimeText => Time.ToString("HH:mm");
    }
}
