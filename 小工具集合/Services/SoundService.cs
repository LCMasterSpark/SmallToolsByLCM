// 文件作用：加载并播放内置 UI 音效资源，供主窗口交互反馈使用。
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Resources;

namespace 小工具集合.Services;

public enum UiSound
{
    Click,
    Hover,
    Execute,
    Success,
    Error
}

public sealed class SoundService
{
    private readonly Dictionary<UiSound, SoundPlayer> players = [];
    private readonly List<MemoryStream> streams = [];

    public SoundService()
    {
        Load(UiSound.Click, "click.wav");
        Load(UiSound.Hover, "hover.wav");
        Load(UiSound.Execute, "execute.wav");
        Load(UiSound.Success, "success.wav");
        Load(UiSound.Error, "error.wav");
    }

    public bool IsEnabled { get; set; } = true;

    public void Play(UiSound sound)
    {
        if (!IsEnabled || !players.TryGetValue(sound, out SoundPlayer? player))
        {
            return;
        }

        try
        {
            player.Play();
        }
        catch
        {
            // UI sounds are optional feedback; audio device failures must never affect tools.
        }
    }

    private void Load(UiSound sound, string fileName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/Sounds/KenneyUI/{fileName}", UriKind.Absolute);
            StreamResourceInfo? resource = Application.GetResourceStream(uri);
            if (resource is null)
            {
                return;
            }

            var memory = new MemoryStream();
            resource.Stream.CopyTo(memory);
            memory.Position = 0;
            var player = new SoundPlayer(memory);
            player.Load();

            streams.Add(memory);
            players[sound] = player;
        }
        catch
        {
            // Missing or invalid resources should degrade to silent UI feedback.
        }
    }
}
