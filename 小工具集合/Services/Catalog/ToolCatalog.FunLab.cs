// 文件作用：注册趣味实验室分组的离线、联网和互动工具元数据。
using 小工具集合.Models;

namespace 小工具集合.Services;

public static partial class ToolCatalog
{
    private static ToolDefinition ChoicePicker() => new()
    {
        Id = "choicePicker",
        Name = "选择困难救星",
        GroupName = "趣味实验室",
        Description = "从多行候选中随机抽一个结果。",
        InputWatermark = "每行输入一个候选项。",
        Operations = [new() { Id = "pick", Name = "随机选择" }],
        Parameters =
        [
            new() { Id = "trimEmpty", Name = "去空行", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "distinct", Name = "去重", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" }
        ]
    };

    private static ToolDefinition ShuffleLines() => new()
    {
        Id = "shuffleLines",
        Name = "随机打乱行",
        GroupName = "趣味实验室",
        Description = "将多行文本随机重新排序。",
        InputWatermark = "每行输入一个项目。",
        Operations = [new() { Id = "shuffle", Name = "打乱" }],
        Parameters =
        [
            new() { Id = "trimEmpty", Name = "去空行", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "distinct", Name = "去重", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition RandomNumber() => new()
    {
        Id = "randomNumber",
        Name = "随机数字",
        GroupName = "趣味实验室",
        Description = "生成指定范围内的随机整数。",
        InputWatermark = "无需输入；请在参数区设置范围。",
        RequiresInput = false,
        Operations = [new() { Id = "generate", Name = "生成" }],
        Parameters =
        [
            new() { Id = "min", Name = "最小值", Kind = ToolParameterKind.Number, DefaultValue = "1" },
            new() { Id = "max", Name = "最大值", Kind = ToolParameterKind.Number, DefaultValue = "100" },
            new() { Id = "count", Name = "数量", Kind = ToolParameterKind.Number, DefaultValue = "1" },
            new() { Id = "unique", Name = "不重复", Kind = ToolParameterKind.CheckBox }
        ]
    };

    private static ToolDefinition DiceRoller() => new()
    {
        Id = "diceRoller",
        Name = "骰子工具",
        GroupName = "趣味实验室",
        Description = "掷骰表达式，例如 1d6、2d20+3。",
        InputWatermark = "请输入骰子表达式，例如 2d6+1。",
        Operations = [new() { Id = "roll", Name = "开骰" }]
    };

    private static ToolDefinition ReverseText() => new()
    {
        Id = "reverseText",
        Name = "倒放文本",
        GroupName = "趣味实验室",
        Description = "将文本整体、逐行或行序倒放。",
        InputWatermark = "请输入要倒放的文本。",
        Operations = [new() { Id = "reverse", Name = "倒放" }],
        Parameters = [new() { Id = "mode", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "全文倒放", Options = ["全文倒放", "逐行倒放", "行序倒放"] }]
    };

    private static ToolDefinition EmojiWrap() => new()
    {
        Id = "emojiWrap",
        Name = "Emoji 装饰",
        GroupName = "趣味实验室",
        Description = "给每行文本加一点随机 Emoji 气氛。",
        InputWatermark = "请输入要装饰的文本。",
        Operations = [new() { Id = "wrap", Name = "装饰" }],
        Parameters =
        [
            new() { Id = "style", Name = "风格", Kind = ToolParameterKind.Combo, DefaultValue = "随机", Options = ["随机", "开心", "星星", "办公"] },
            new() { Id = "bothSides", Name = "两侧装饰", Kind = ToolParameterKind.CheckBox, DefaultValue = "true" },
            new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }
        ]
    };

    private static ToolDefinition MockingText() => new()
    {
        Id = "mockingText",
        Name = "阴阳怪气转换",
        GroupName = "趣味实验室",
        Description = "把文本转换成大小写交替或带波浪的语气。",
        InputWatermark = "请输入要整活的文本。",
        Operations = [new() { Id = "mock", Name = "转换" }],
        Parameters = [new() { Id = "mode", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "交替大小写", Options = ["交替大小写", "随机大小写", "加波浪"] }]
    };

    private static ToolDefinition ZalgoText() => new()
    {
        Id = "zalgoText",
        Name = "故障文本",
        GroupName = "趣味实验室",
        Description = "给文本增加轻量故障效果。",
        InputWatermark = "请输入要故障化的文本。",
        Operations = [new() { Id = "glitch", Name = "故障化" }],
        Parameters = [new() { Id = "strength", Name = "强度", Kind = ToolParameterKind.Combo, DefaultValue = "低", Options = ["低", "中", "高"] }]
    };

    private static ToolDefinition CommitMessage() => new()
    {
        Id = "commitMessage",
        Name = "Commit 文案",
        GroupName = "趣味实验室",
        Description = "根据改动摘要生成规则化 commit 文案候选。",
        InputWatermark = "请输入改动摘要，例如：新增 URL 拆解和端口查询。",
        Operations = [new() { Id = "generate", Name = "生成文案" }],
        Parameters =
        [
            new() { Id = "language", Name = "语言", Kind = ToolParameterKind.Combo, DefaultValue = "中文", Options = ["中文", "English"] },
            new() { Id = "type", Name = "类型", Kind = ToolParameterKind.Combo, DefaultValue = "自动", Options = ["自动", "feat", "fix", "refactor", "docs", "chore"] },
            new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }
        ]
    };

    private static ToolDefinition VariableName() => new()
    {
        Id = "variableName",
        Name = "变量名灵感",
        GroupName = "趣味实验室",
        Description = "从短语生成多种命名风格候选。",
        InputWatermark = "请输入中文、英文或混合短语。",
        Operations = [new() { Id = "generate", Name = "生成变量名" }],
        Parameters = [new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }]
    };

    private static ToolDefinition FakeLog() => new()
    {
        Id = "fakeLog",
        Name = "假日志生成器",
        GroupName = "趣味实验室",
        Description = "生成本地离线的假日志/假错误。",
        InputWatermark = "可选：输入日志主题；留空则随机生成。",
        Operations = [new() { Id = "generate", Name = "生成日志" }],
        Parameters =
        [
            new() { Id = "level", Name = "级别", Kind = ToolParameterKind.Combo, DefaultValue = "混合", Options = ["混合", "INFO", "WARN", "ERROR", "DEBUG"] },
            new() { Id = "count", Name = "行数", Kind = ToolParameterKind.Number, DefaultValue = "8" },
            new() { Id = "service", Name = "服务名", Kind = ToolParameterKind.Text, DefaultValue = "fun-lab" },
            new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }
        ]
    };

    private static ToolDefinition ExcuseGenerator() => new()
    {
        Id = "excuseGenerator",
        Name = "程序员借口",
        GroupName = "趣味实验室",
        Description = "生成一个完全离线、只供娱乐的程序员借口。",
        InputWatermark = "无需输入；想指定场景也可以写一句。",
        Operations = [new() { Id = "generate", Name = "生成借口" }],
        Parameters = [new() { Id = "enhancement", Name = "模式", Kind = ToolParameterKind.Combo, DefaultValue = "本地", Options = ["本地", "联网增强"] }]
    };

    private static ToolDefinition OnlineHitokoto() => new()
    {
        Id = "onlineHitokoto",
        Name = "随机一言",
        GroupName = "趣味实验室",
        Description = "从 Hitokoto 获取一句随机短句，并标注来源。",
        InputWatermark = "无需输入；请在参数区选择分类。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "来一句" }],
        Parameters =
        [
            new()
            {
                Id = "category",
                Name = "分类",
                Kind = ToolParameterKind.Combo,
                DefaultValue = "随机",
                Options = ["随机", "动画", "漫画", "游戏", "文学", "原创", "网络", "影视", "诗词", "哲学", "抖机灵"]
            }
        ]
    };

    private static ToolDefinition OnlinePoemLine() => new()
    {
        Id = "onlinePoemLine",
        Name = "诗词一句",
        GroupName = "趣味实验室",
        Description = "从免 Key 接口获取一句诗词分类短句。",
        InputWatermark = "无需输入；点击执行后联网获取。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "取一句" }]
    };

    private static ToolDefinition WeatherCard() => new()
    {
        Id = "weatherCard",
        Name = "天气小卡",
        GroupName = "趣味实验室",
        Description = "使用 Open-Meteo 查询城市或经纬度的当前天气。",
        InputWatermark = "输入城市名或纬度,经度，例如：北京 或 39.9,116.4。",
        Operations = [new() { Id = "query", Name = "查询天气" }]
    };

    private static ToolDefinition IpInfoCard() => new()
    {
        Id = "ipInfoCard",
        Name = "IP 信息小卡",
        GroupName = "趣味实验室",
        Description = "查询当前出口 IP、指定 IP 或域名的地理和运营商信息。",
        InputWatermark = "可留空查询当前出口 IP；也可输入 IP 或域名，例如 8.8.8.8。",
        RequiresInput = false,
        Operations = [new() { Id = "query", Name = "查询 IP" }]
    };

    private static ToolDefinition PowerChecker() => new()
    {
        Id = "powerChecker",
        Name = "电量检测器",
        GroupName = "趣味实验室",
        Description = "使用 LCMasterSpark 高精度存在性判定引擎检测这台电脑是否有电。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "powerChecker"
    };

    private static ToolDefinition TimePointer() => new()
    {
        Id = "timePointer",
        Name = "时间显示器",
        GroupName = "趣味实验室",
        Description = "启动高精度时间感知分析，并用红色箭头指向任务栏时间。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "timePointer"
    };

    private static ToolDefinition Minesweeper() => new()
    {
        Id = "minesweeper",
        Name = "扫雷",
        GroupName = "趣味实验室",
        Description = "内嵌版扫雷小游戏，保留普通模式、街机模式和本次运行内战绩。",
        RequiresInput = false,
        Operations = [new() { Id = "interactive", Name = "互动" }],
        InteractiveViewKey = "minesweeper"
    };
}
