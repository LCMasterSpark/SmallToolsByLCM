// 文件作用：实现电量检测器的进度动画、状态文案和生命周期。
using System.Windows;
using System.Windows.Controls;

namespace 小工具集合.Views.FunLab;

public partial class PowerCheckerControl : UserControl, IInteractiveToolView, IDisposable
{
    private static readonly string[] AnalysisMessages =
    [
        "大模型分析中...",
        "探查宇宙量子概率云状态...",
        "读取摩尔斯悖论...",
        "校准电子存在感参数...",
        "采样主板电流叙事结构...",
        "查询薛定谔电源适配器状态...",
        "折叠高维电量波函数...",
        "同步银河系插座拓扑图...",
        "解析量子伏特浮点误差...",
        "比对宇宙背景辐射供电曲线...",
        "调用 LCMasterSpark 定律验证器...",
        "扫描 CPU 梦境残留电荷...",
        "读取内存条灵感脉冲...",
        "验证屏幕发光的因果合理性...",
        "生成最终供电可信度画像..."
    ];

    private readonly Random random = new();
    private readonly List<string> messageBag = [];
    private CancellationTokenSource? runCancellation;

    public PowerCheckerControl()
    {
        InitializeComponent();
        Unloaded += (_, _) => Deactivate();
    }

    public void Deactivate()
    {
        runCancellation?.Cancel();
        runCancellation?.Dispose();
        runCancellation = null;
    }

    public void Dispose()
    {
        Deactivate();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        Deactivate();
        runCancellation = new CancellationTokenSource();
        CancellationToken token = runCancellation.Token;

        StartButton.IsEnabled = false;
        StartButton.Content = "检测中";
        AnalysisProgress.Value = 0;
        StallText.Text = "";
        MainStatusText.Text = "大模型深度学习思考分析中";
        DetailStatusText.Text = GetNextAnalysisMessage();

        try
        {
            await RunAnalysisAsync(token);
            AnalysisProgress.Value = 100;
            MainStatusText.Text = "分析完成";
            DetailStatusText.Text = "根据 LCMasterSpark 定律，这台电脑有电。";
            StallText.Text = "";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                StartButton.IsEnabled = true;
                StartButton.Content = "再次检测";
            }
        }
    }

    private async Task RunAnalysisAsync(CancellationToken token)
    {
        int durationMilliseconds = random.Next(8_000, 12_001);
        const int tickMilliseconds = 24;
        const int messageIntervalMilliseconds = 900;
        int elapsedMilliseconds = 0;
        int messageElapsedMilliseconds = messageIntervalMilliseconds;
        int nextStallCheckMilliseconds = random.Next(700, 1_801);
        bool hasStalledAtNinetyEightPercent = false;
        double speedFactor = 1;
        int nextSpeedChangeMilliseconds = random.Next(450, 1_201);

        while (elapsedMilliseconds < durationMilliseconds)
        {
            token.ThrowIfCancellationRequested();
            if (!hasStalledAtNinetyEightPercent && AnalysisProgress.Value >= 98)
            {
                hasStalledAtNinetyEightPercent = true;
                await StallAsync(random.Next(2_500, 5_501), token);
                nextStallCheckMilliseconds = elapsedMilliseconds + random.Next(700, 1_801);
            }

            if (elapsedMilliseconds >= nextStallCheckMilliseconds && AnalysisProgress.Value < 96)
            {
                int stallMilliseconds = random.Next(0, 2_801);
                if (stallMilliseconds > 0 && random.NextDouble() < 0.42)
                {
                    await StallAsync(stallMilliseconds, token);
                }

                nextStallCheckMilliseconds = elapsedMilliseconds + random.Next(700, 1_801);
            }

            if (messageElapsedMilliseconds >= messageIntervalMilliseconds)
            {
                DetailStatusText.Text = GetNextAnalysisMessage();
                messageElapsedMilliseconds = 0;
            }

            await Task.Delay(tickMilliseconds, token);
            if (elapsedMilliseconds >= nextSpeedChangeMilliseconds)
            {
                speedFactor = 0.88 + random.NextDouble() * 0.24;
                nextSpeedChangeMilliseconds = elapsedMilliseconds + random.Next(450, 1_201);
            }

            elapsedMilliseconds += (int)Math.Round(tickMilliseconds * speedFactor);
            messageElapsedMilliseconds += tickMilliseconds;
            AnalysisProgress.Value = Math.Min(100, elapsedMilliseconds * 100.0 / durationMilliseconds);
        }
    }

    private async Task StallAsync(int stallMilliseconds, CancellationToken token)
    {
        string originalStatus = DetailStatusText.Text;
        string baseStatus = originalStatus.TrimEnd('.', '。');
        StallText.Text = stallMilliseconds >= 2_200 ? "本步骤耗时较长，请耐心等待" : "";

        int elapsedMilliseconds = 0;
        int dotCount = 1;
        const int ellipsisTickMilliseconds = 350;
        while (elapsedMilliseconds < stallMilliseconds)
        {
            token.ThrowIfCancellationRequested();
            DetailStatusText.Text = baseStatus + new string('.', dotCount);
            dotCount = dotCount == 3 ? 1 : dotCount + 1;
            int delayMilliseconds = Math.Min(ellipsisTickMilliseconds, stallMilliseconds - elapsedMilliseconds);
            await Task.Delay(delayMilliseconds, token);
            elapsedMilliseconds += delayMilliseconds;
        }

        DetailStatusText.Text = originalStatus;
        StallText.Text = "";
    }

    private string GetNextAnalysisMessage()
    {
        if (messageBag.Count == 0)
        {
            messageBag.AddRange(AnalysisMessages);
            Shuffle(messageBag);
        }

        string message = messageBag[^1];
        messageBag.RemoveAt(messageBag.Count - 1);
        return message;
    }

    private void Shuffle(List<string> messages)
    {
        for (int i = messages.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (messages[i], messages[j]) = (messages[j], messages[i]);
        }
    }
}
