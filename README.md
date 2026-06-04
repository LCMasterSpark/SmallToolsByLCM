# LCM的工具箱

![Version](https://img.shields.io/badge/version-2.8rel-007ACC?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF%20%2B%20MahApps-5C2D91?style=flat-square)

一个面向 Windows 的本地开发者工具箱。  
作者：**LCMasterSpark**

它把日常会反复打开网页、命令行、小脚本才能完成的小任务收进一个 WPF 桌面应用里：编码转换、文本格式化、摘要加密、网络诊断、文件批处理、随机生成，以及一些好玩的“趣味实验室”工具。

从 2.8rel 开始，应用正式更名为 **LCM的工具箱**。界面采用 Visual Studio 2022 深色工作台风格，支持工具搜索、折叠导航、互动工具、在线趣味小卡和本地状态保存。

## Highlights

- **VS2022 风格工作台**：深色 Activity Bar、工具浏览器、命令栏、编辑器式输入/输出区、右侧属性面板。
- **工具搜索**：按工具名称、描述、分组快速定位工具。
- **本地优先**：大多数工具离线运行，不保存输入正文、密钥或输出历史。
- **在线增强**：天气、IP 信息、一言、诗词、变量名灵感、Commit 文案等功能可调用公开接口增强。
- **互动工具**：扫雷、电量检测器、时间显示器、屏幕指针、QR Code 生成器。
- **批处理友好**：文件加密、MP3 提取、图片转换/压缩、文件哈希等工具支持队列和暂停。
- **单文件发布**：支持发布为 Windows x64 自包含单文件，可直接运行。

## Tool Groups

| 分组 | 代表功能 |
| --- | --- |
| 编码转换 | Base64、URL 工具、HTML 实体、Unicode 转义 |
| 文本格式化 | JSON/XML 格式化与压缩、JWT 解析、正则测试、文本 diff |
| 加密摘要 | SHA-256、SHA-512、MD5、HMAC、AES-GCM、RSA-OAEP |
| 文件与批处理 | 文件加密、MP4 提取 MP3、图片格式转换、文件哈希、图片压缩 |
| 网络与接口 | HTTP 请求、URL 参数解析、Ping、端口连通、端口占用、DNS、公网 IP、curl 生成 |
| 生成工具 | UUID、时间戳、密码生成器、屏幕指针、生成 QR Code |
| 趣味实验室 | 随机选择、打乱行、随机数字、骰子、倒放文本、Emoji 装饰、Commit 文案、变量名灵感、假日志、程序员借口、扫雷等 |
| 在线小卡 | 随机一言、诗词一句、天气小卡、IP 信息小卡 |

## Online Features

联网功能默认只在对应工具或“联网增强”模式下触发。当前使用的公开接口包括：

- Hitokoto：随机一言、诗词一句、趣味灵感。
- Open-Meteo：天气小卡和城市地理编码。
- IP-API：IP 信息小卡。
- MyMemory：变量名和 Commit 文案的轻量翻译辅助。

这些能力定位为轻量辅助；网络失败时，支持回退本地结果的工具会保留本地输出并提示失败原因。

## Build

需要：

- Windows
- .NET SDK 10

构建：

```powershell
dotnet build 小工具集合.slnx --no-restore
```

测试：

```powershell
dotnet test 小工具集合.slnx
```

发布 Windows x64 自包含单文件：

```powershell
dotnet publish 小工具集合/小工具集合.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:Version=2.8.0 `
  -p:InformationalVersion=2.8rel
```

发布后 exe 名称为 `LCM的工具箱.exe`。

## Project Structure

```text
小工具集合/
  Models/                 工具定义、请求和返回模型
  Services/
    ToolCatalog.cs        工具目录和参数元数据
    ToolProcessor.cs      工具调度入口
    ToolProcessors/       按功能拆分的 partial 工具处理器
  ViewModels/             主窗口状态、命令和偏好协调
  Views/
    Generation/           生成工具互动控件
    FunLab/               趣味实验室互动控件
  MainWindow.xaml         VS2022 风格主界面
  MainWindow.xaml.cs      动态参数控件和窗口交互

小工具集合.Tests/
  xUnit 单元测试和轻量集成测试
```

## Privacy

本工具箱不保存输入正文、密钥或执行历史。  
需要联网的工具会把当前输入发送到对应公开接口；如果不希望发送内容，请保持工具为本地模式。

## License

MIT License. See [LICENSE](LICENSE).

## Author

Made by **LCMasterSpark**.
