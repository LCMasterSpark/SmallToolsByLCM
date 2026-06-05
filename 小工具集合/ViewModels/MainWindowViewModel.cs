// 文件作用：承载主窗口状态、工具选择、搜索、命令执行和偏好保存。
using System.Collections.ObjectModel;
using System.Windows.Input;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed class ToolBrowserItem
{
    public required ToolGroup Group { get; init; }
    public required ToolDefinition Tool { get; init; }
}

/// <summary>
/// 协调主窗口中的工具选择、用户输入、命令状态和偏好持久化。
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IToolProcessor _processor;
    private readonly PreferenceService _preferenceService;
    private readonly TaskHistoryService _taskHistoryService;
    private readonly UpdateCheckService _updateCheckService;
    private readonly TranslationSettingsService _translationSettingsService;
    private readonly AppPreferences _preferences;
    private readonly TranslationSettings _translationSettings;
    private ToolGroup _selectedGroup;
    private ToolDefinition _selectedTool;
    private ToolOperation _selectedOperation;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _statusText = "就绪";
    private string _searchText = string.Empty;
    private string _updateStatusText = "尚未检查更新。";
    private bool _isSuccess = true;
    private bool _isBusy;
    private bool _isPaused;
    private readonly AsyncRelayCommand _executeCommand;
    private readonly AsyncRelayCommand _checkUpdateCommand;
    private readonly AsyncRelayCommand<TaskHistoryRecord> _retryTaskCommand;

    public MainWindowViewModel()
    {
        _processor = new ToolProcessor();
        _preferenceService = new PreferenceService();
        _taskHistoryService = new TaskHistoryService();
        _updateCheckService = new UpdateCheckService();
        _translationSettingsService = new TranslationSettingsService();
        _preferences = _preferenceService.Load();
        _translationSettings = _translationSettingsService.Load();
        Groups = ToolCatalog.Groups;

        // 尽量恢复上次选择的工具，再从静态目录推导出当前分组和第一个操作。
        ToolDefinition savedTool = ToolCatalog.FindTool(_preferences.LastToolId);
        if (!_preferences.RestoreLastToolOnStartup)
        {
            savedTool = ToolCatalog.DefaultTool;
        }

        _selectedGroup = Groups.FirstOrDefault(group => group.Tools.Contains(savedTool)) ?? Groups[0];
        _selectedTool = savedTool;
        _selectedOperation = _selectedTool.Operations[0];
        Parameters = [];
        TaskHistory = new ObservableCollection<TaskHistoryRecord>(_taskHistoryService.Load());
        RebuildParameters();

        _executeCommand = new AsyncRelayCommand(ExecuteAsync, () => !IsBusy);
        _checkUpdateCommand = new AsyncRelayCommand(CheckUpdateAsync, () => !IsBusy);
        _retryTaskCommand = new AsyncRelayCommand<TaskHistoryRecord>(RetryTaskAsync, _ => !IsBusy);
        ExecuteCommand = _executeCommand;
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
        ClearCommand = new RelayCommand(Clear);
        ClearRecentCommand = new RelayCommand(ClearRecentTools);
        ClearTaskHistoryCommand = new RelayCommand(ClearTaskHistory);
        CheckUpdateCommand = _checkUpdateCommand;
        RetryTaskCommand = _retryTaskCommand;
        PauseCommand = new RelayCommand(TogglePause, () => IsBusy && IsPausableTool);
    }

    public IReadOnlyList<ToolGroup> Groups { get; }

    public ObservableCollection<ToolParameterValue> Parameters { get; }

    public ObservableCollection<TaskHistoryRecord> TaskHistory { get; }

    public ICommand ExecuteCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand ClearRecentCommand { get; }

    public ICommand ClearTaskHistoryCommand { get; }

    public ICommand CheckUpdateCommand { get; }

    public ICommand RetryTaskCommand { get; }

    public ICommand PauseCommand { get; }

    public event EventHandler<bool>? ToolExecutionCompleted;

    public ToolGroup SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (value is null || value.Tools.Count == 0)
            {
                return;
            }

            if (SetProperty(ref _selectedGroup, value))
            {
                SelectedTool = value.Tools[0];
                OnPropertyChanged(nameof(AvailableTools));
                OnPropertyChanged(nameof(VisibleToolItems));
                StatusText = $"已切换到：{value.Name}";
            }
        }
    }

    public IReadOnlyList<ToolDefinition> AvailableTools => SelectedGroup.Tools;

    public IReadOnlyList<ToolBrowserItem> VisibleToolItems
    {
        get
        {
            string query = SearchText.Trim();
            IEnumerable<ToolGroup> groups = string.IsNullOrWhiteSpace(query) ? [SelectedGroup] : Groups;
            return groups
                .SelectMany(group => group.Tools
                    .Where(tool => ToolMatchesSearch(group, tool, query))
                    .Select(tool => new ToolBrowserItem { Group = group, Tool = tool }))
                .ToList();
        }
    }

    public ToolDefinition SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (value is null || value.Operations.Count == 0)
            {
                return;
            }

            if (SetProperty(ref _selectedTool, value))
            {
                SelectedOperation = value.Operations[0];
                RebuildParameters();
                if (!value.RequiresInput)
                {
                    InputText = string.Empty;
                }

                OutputText = string.Empty;
                StatusText = $"已选择：{value.Name}";
                _preferences.LastToolId = value.Id;
                AddRecentTool(value.Id);
                SavePreferences();
                OnPropertyChanged(nameof(FavoriteToolItems));
                OnPropertyChanged(nameof(RecentToolItems));
                OnPropertyChanged(nameof(HasFavoriteTools));
                OnPropertyChanged(nameof(HasRecentTools));
                OnPropertyChanged(nameof(IsSelectedToolFavorite));
                OnPropertyChanged(nameof(HasInput));
                OnPropertyChanged(nameof(HasInteractiveView));
                OnPropertyChanged(nameof(IsStandardTool));
                OnPropertyChanged(nameof(IsPausableTool));
                OnPropertyChanged(nameof(HasQueuedWork));
                OnPropertyChanged(nameof(PauseButtonText));
                OnPropertyChanged(nameof(HasWarning));
                OnPropertyChanged(nameof(WarningText));
                RaiseCommandStates();
            }
        }
    }

    public ToolOperation SelectedOperation
    {
        get => _selectedOperation;
        set => SetProperty(ref _selectedOperation, value);
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public string OutputText
    {
        get => _outputText;
        set => SetProperty(ref _outputText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(VisibleToolItems));
            }
        }
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public bool HasWarning => !string.IsNullOrWhiteSpace(SelectedTool.Warning);

    public string WarningText => SelectedTool.Warning;

    public IReadOnlyList<ToolBrowserItem> FavoriteToolItems => BuildPinnedToolItems(_preferences.FavoriteToolIds);

    public IReadOnlyList<ToolBrowserItem> RecentToolItems => BuildPinnedToolItems(_preferences.RecentToolIds);

    public bool HasFavoriteTools => FavoriteToolItems.Count > 0;

    public bool HasRecentTools => RecentToolItems.Count > 0;

    public bool IsSelectedToolFavorite => _preferences.FavoriteToolIds.Contains(SelectedTool.Id, StringComparer.Ordinal);

    public bool HasInput => SelectedTool.RequiresInput;

    public bool HasInteractiveView => !string.IsNullOrWhiteSpace(SelectedTool.InteractiveViewKey);

    public bool IsStandardTool => !HasInteractiveView;

    public bool IsPausableTool => IsPausableToolId(SelectedTool.Id);

    // 文件队列即使尚未开始执行也视为待处理工作，
    // 避免关闭程序时误丢已经准备好的批处理列表。
    public bool HasQueuedWork => IsPausableTool && Parameters.Any(parameter =>
        parameter.Definition.Id == "inputFiles" && !string.IsNullOrWhiteSpace(parameter.Value));

    public bool HasActiveOrQueuedWork => IsBusy || HasQueuedWork;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(HasActiveOrQueuedWork));
                OnPropertyChanged(nameof(PauseButtonText));
                RaiseCommandStates();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseButtonText));
            }
        }
    }

    public string PauseButtonText => IsPaused ? "继续" : "暂停";

    public double WindowWidth
    {
        get => _preferences.Width;
        set
        {
            _preferences.Width = value;
            SavePreferences();
        }
    }

    public double WindowHeight
    {
        get => _preferences.Height;
        set
        {
            _preferences.Height = value;
            SavePreferences();
        }
    }

    public bool IsUiSoundEnabled
    {
        get => _preferences.IsUiSoundEnabled;
        set
        {
            if (_preferences.IsUiSoundEnabled == value)
            {
                return;
            }

            _preferences.IsUiSoundEnabled = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public string Theme
    {
        get => _preferences.Theme;
        set
        {
            if (_preferences.Theme == value)
            {
                return;
            }

            _preferences.Theme = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public string DefaultOutputDirectory
    {
        get => _preferences.DefaultOutputDirectory;
        set
        {
            if (_preferences.DefaultOutputDirectory == value)
            {
                return;
            }

            _preferences.DefaultOutputDirectory = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public bool RestoreLastToolOnStartup
    {
        get => _preferences.RestoreLastToolOnStartup;
        set
        {
            if (_preferences.RestoreLastToolOnStartup == value)
            {
                return;
            }

            _preferences.RestoreLastToolOnStartup = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public bool IsNetworkEnabled
    {
        get => _preferences.IsNetworkEnabled;
        set
        {
            if (_preferences.IsNetworkEnabled == value)
            {
                return;
            }

            _preferences.IsNetworkEnabled = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public string OcrSpaceApiKey
    {
        get => _preferences.OcrSpaceApiKey;
        set
        {
            if (_preferences.OcrSpaceApiKey == value)
            {
                return;
            }

            _preferences.OcrSpaceApiKey = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public string TranslationDefaultProvider
    {
        get => _translationSettings.DefaultProvider;
        set
        {
            if (_translationSettings.DefaultProvider == value)
            {
                return;
            }

            _translationSettings.DefaultProvider = value;
            SaveTranslationSettings();
            OnPropertyChanged();
        }
    }

    public string TranslationDefaultSourceLanguage
    {
        get => _translationSettings.DefaultSourceLanguage;
        set
        {
            if (_translationSettings.DefaultSourceLanguage == value)
            {
                return;
            }

            _translationSettings.DefaultSourceLanguage = value;
            SaveTranslationSettings();
            OnPropertyChanged();
        }
    }

    public string TranslationDefaultTargetLanguage
    {
        get => _translationSettings.DefaultTargetLanguage;
        set
        {
            if (_translationSettings.DefaultTargetLanguage == value)
            {
                return;
            }

            _translationSettings.DefaultTargetLanguage = value;
            SaveTranslationSettings();
            OnPropertyChanged();
        }
    }

    public string LibreTranslateEndpoint
    {
        get => _translationSettings.LibreTranslateEndpoint;
        set => SetTranslationString(_translationSettings.LibreTranslateEndpoint, value, v => _translationSettings.LibreTranslateEndpoint = v);
    }

    public string LibreTranslateApiKey
    {
        get => _translationSettings.LibreTranslateApiKey;
        set => SetTranslationString(_translationSettings.LibreTranslateApiKey, value, v => _translationSettings.LibreTranslateApiKey = v);
    }

    public string AzureEndpoint
    {
        get => _translationSettings.AzureEndpoint;
        set => SetTranslationString(_translationSettings.AzureEndpoint, value, v => _translationSettings.AzureEndpoint = v);
    }

    public string AzureRegion
    {
        get => _translationSettings.AzureRegion;
        set => SetTranslationString(_translationSettings.AzureRegion, value, v => _translationSettings.AzureRegion = v);
    }

    public string AzureKey
    {
        get => _translationSettings.AzureKey;
        set => SetTranslationString(_translationSettings.AzureKey, value, v => _translationSettings.AzureKey = v);
    }

    public string DeepLApiUrl
    {
        get => _translationSettings.DeepLApiUrl;
        set => SetTranslationString(_translationSettings.DeepLApiUrl, value, v => _translationSettings.DeepLApiUrl = v);
    }

    public string DeepLApiKey
    {
        get => _translationSettings.DeepLApiKey;
        set => SetTranslationString(_translationSettings.DeepLApiKey, value, v => _translationSettings.DeepLApiKey = v);
    }

    public string GoogleApiKey
    {
        get => _translationSettings.GoogleApiKey;
        set => SetTranslationString(_translationSettings.GoogleApiKey, value, v => _translationSettings.GoogleApiKey = v);
    }

    public string BaiduAppId
    {
        get => _translationSettings.BaiduAppId;
        set => SetTranslationString(_translationSettings.BaiduAppId, value, v => _translationSettings.BaiduAppId = v);
    }

    public string BaiduSecret
    {
        get => _translationSettings.BaiduSecret;
        set => SetTranslationString(_translationSettings.BaiduSecret, value, v => _translationSettings.BaiduSecret = v);
    }

    public string YoudaoAppKey
    {
        get => _translationSettings.YoudaoAppKey;
        set => SetTranslationString(_translationSettings.YoudaoAppKey, value, v => _translationSettings.YoudaoAppKey = v);
    }

    public string YoudaoAppSecret
    {
        get => _translationSettings.YoudaoAppSecret;
        set => SetTranslationString(_translationSettings.YoudaoAppSecret, value, v => _translationSettings.YoudaoAppSecret = v);
    }

    public string OpenAiBaseUrl
    {
        get => _translationSettings.OpenAiBaseUrl;
        set => SetTranslationString(_translationSettings.OpenAiBaseUrl, value, v => _translationSettings.OpenAiBaseUrl = v);
    }

    public string OpenAiApiKey
    {
        get => _translationSettings.OpenAiApiKey;
        set => SetTranslationString(_translationSettings.OpenAiApiKey, value, v => _translationSettings.OpenAiApiKey = v);
    }

    public string OpenAiModel
    {
        get => _translationSettings.OpenAiModel;
        set => SetTranslationString(_translationSettings.OpenAiModel, value, v => _translationSettings.OpenAiModel = v);
    }

    public string OllamaEndpoint
    {
        get => _translationSettings.OllamaEndpoint;
        set => SetTranslationString(_translationSettings.OllamaEndpoint, value, v => _translationSettings.OllamaEndpoint = v);
    }

    public string OllamaModel
    {
        get => _translationSettings.OllamaModel;
        set => SetTranslationString(_translationSettings.OllamaModel, value, v => _translationSettings.OllamaModel = v);
    }

    public bool TranslationUseGlossary
    {
        get => _translationSettings.UseGlossary;
        set
        {
            if (_translationSettings.UseGlossary == value)
            {
                return;
            }

            _translationSettings.UseGlossary = value;
            SaveTranslationSettings();
            OnPropertyChanged();
        }
    }

    public string TranslationGlossary
    {
        get => _translationSettings.Glossary;
        set => SetTranslationString(_translationSettings.Glossary, value, v => _translationSettings.Glossary = v);
    }

    public int LiveRefreshMilliseconds
    {
        get => _translationSettings.LiveRefreshMilliseconds;
        set
        {
            int clamped = Math.Clamp(value, 500, 10000);
            if (_translationSettings.LiveRefreshMilliseconds == clamped)
            {
                return;
            }

            _translationSettings.LiveRefreshMilliseconds = clamped;
            SaveTranslationSettings();
            OnPropertyChanged();
        }
    }

    public bool SaveTaskHistory
    {
        get => _preferences.SaveTaskHistory;
        set
        {
            if (_preferences.SaveTaskHistory == value)
            {
                return;
            }

            _preferences.SaveTaskHistory = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public int TaskHistoryLimit
    {
        get => _preferences.TaskHistoryLimit;
        set
        {
            int clamped = Math.Clamp(value, 1, 500);
            if (_preferences.TaskHistoryLimit == clamped)
            {
                return;
            }

            _preferences.TaskHistoryLimit = clamped;
            SavePreferences();
            OnPropertyChanged();
        }
    }

}
