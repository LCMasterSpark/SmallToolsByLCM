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
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IToolProcessor _processor;
    private readonly PreferenceService _preferenceService;
    private readonly AppPreferences _preferences;
    private ToolGroup _selectedGroup;
    private ToolDefinition _selectedTool;
    private ToolOperation _selectedOperation;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _statusText = "就绪";
    private string _searchText = string.Empty;
    private bool _isSuccess = true;
    private bool _isBusy;
    private bool _isPaused;
    private readonly AsyncRelayCommand _executeCommand;

    public MainWindowViewModel()
    {
        _processor = new ToolProcessor();
        _preferenceService = new PreferenceService();
        _preferences = _preferenceService.Load();
        Groups = ToolCatalog.Groups;

        // 尽量恢复上次选择的工具，再从静态目录推导出当前分组和第一个操作。
        ToolDefinition savedTool = ToolCatalog.FindTool(_preferences.LastToolId);
        _selectedGroup = Groups.FirstOrDefault(group => group.Tools.Contains(savedTool)) ?? Groups[0];
        _selectedTool = savedTool;
        _selectedOperation = _selectedTool.Operations[0];
        Parameters = [];
        RebuildParameters();

        _executeCommand = new AsyncRelayCommand(ExecuteAsync, () => !IsBusy);
        ExecuteCommand = _executeCommand;
        ClearCommand = new RelayCommand(Clear);
        PauseCommand = new RelayCommand(TogglePause, () => IsBusy && IsPausableTool);
    }

    public IReadOnlyList<ToolGroup> Groups { get; }

    public ObservableCollection<ToolParameterValue> Parameters { get; }

    public ICommand ExecuteCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand PauseCommand { get; }

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
                SavePreferences();
                OnPropertyChanged(nameof(HasInput));
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

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public bool HasWarning => !string.IsNullOrWhiteSpace(SelectedTool.Warning);

    public string WarningText => SelectedTool.Warning;

    public bool HasInput => SelectedTool.RequiresInput;

    public bool IsPausableTool => SelectedTool.Id is "fileEncode" or "mp4ToMp3" or "imageConvert" or "fileHash" or "imageCompress";

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

    public async Task ExecuteAsync()
    {
        // 将运行时参数摊平成字典，避免 ToolProcessor 依赖 WPF 控件或绑定对象。
        var request = new ToolRequest
        {
            ToolId = SelectedTool.Id,
            OperationId = SelectedOperation.Id,
            Input = HasInput ? InputText : string.Empty,
            Parameters = Parameters.ToDictionary(parameter => parameter.Definition.Id, parameter => parameter.Value)
        };

        IsBusy = true;
        IsPaused = false;
        StatusText = "正在处理...";

        try
        {
            ToolResult result = await _processor.ExecuteAsync(request, new ToolExecutionContext(() => IsPaused));
            IsSuccess = result.Success;
            OutputText = result.Success ? result.Output : string.Empty;
            StatusText = result.Message;
        }
        finally
        {
            IsPaused = false;
            IsBusy = false;
            OnPropertyChanged(nameof(HasQueuedWork));
            OnPropertyChanged(nameof(HasActiveOrQueuedWork));
        }
    }

    public void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        RebuildParameters();

        StatusText = "已清空";
        IsSuccess = true;
        OnPropertyChanged(nameof(HasQueuedWork));
        OnPropertyChanged(nameof(HasActiveOrQueuedWork));
    }

    public void SelectToolItem(ToolBrowserItem item)
    {
        if (item.Group != SelectedGroup)
        {
            SelectedGroup = item.Group;
        }

        SelectedTool = item.Tool;
        OnPropertyChanged(nameof(VisibleToolItems));
    }

    private void RebuildParameters()
    {
        // 切换工具时重建运行时参数集合，UI 层会据此重新生成对应控件。
        Parameters.Clear();
        foreach (ToolParameterDefinition definition in SelectedTool.Parameters)
        {
            Parameters.Add(new ToolParameterValue
            {
                Definition = definition,
                Value = definition.DefaultValue
            });
        }
    }

    private void SavePreferences()
    {
        _preferenceService.Save(_preferences);
    }

    private void TogglePause()
    {
        if (!IsBusy || !IsPausableTool)
        {
            return;
        }

        IsPaused = !IsPaused;
        StatusText = IsPaused ? "已暂停，当前文件处理完成后会停在下一个文件前。" : "继续处理...";
    }

    private static bool ToolMatchesSearch(ToolGroup group, ToolDefinition tool, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || group.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tool.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tool.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseCommandStates()
    {
        _executeCommand.RaiseCanExecuteChanged();
        if (PauseCommand is RelayCommand pauseCommand)
        {
            pauseCommand.RaiseCanExecuteChanged();
        }
    }
}
