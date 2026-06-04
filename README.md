# 小工具集合

![Version](https://img.shields.io/badge/version-2.0--beta-007ACC?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square)
![UI](https://img.shields.io/badge/UI-WPF%20%2B%20MahApps-5C2D91?style=flat-square)

一个面向 Windows 的本地开发者工具箱。  
作者：**LCMasterSpark**

它把日常会反复打开网页、命令行、小脚本才能完成的小任务收进一个 WPF 桌面应用里：编码转换、文本格式化、摘要加密、网络诊断、文件批处理、随机生成，以及一点好玩的“趣味实验室”。

2.0 beta 开始，界面改成了偏 Visual Studio 2022 的深色工作台风格，并加入工具搜索、折叠导航和联网趣味增强。

## Highlights

- **VS2022 风格工作台**：深色 Activity Bar、工具浏览器、命令栏、编辑器式输入/输出区、右侧属性面板。
- **工具搜索**：按工具名、描述、分组快速定位工具。
- **本地优先**：绝大多数工具离线运行，不保存输入、密钥或历史记录。
- **联网增强可选**：天气、IP 信息、一言、诗词、变量名灵感、Commit 文案等功能可调用公开接口增强。
- **批处理友好**：文件加密、MP3 提取、图片转换/压缩、文件哈希等批处理工具支持队列和暂停。
- **单文件发布**：支持发布为 Windows x64 自包含单文件，可直接运行。

## Tool Groups

| 分组 | 代表功能 |
| --- | --- |
| 编码转换 | Base64、URL 工具、HTML 实体、Unicode 转义 |
| 文本格式化 | JSON/XML 格式化与压缩、JWT 解析、正则测试、文本 diff |
| 加密摘要 | SHA-256、SHA-512、MD5、HMAC、AES-GCM、RSA-OAEP |
| 文件与批处理 | 文件加密、MP4 提取 MP3、图片格式转换、文件哈希、图片压缩 |
| 网络与接口 | HTTP 请求、URL 参数解析、Ping、端口连通、端口占用、DNS、公网 IP、curl 生成 |
| 生成工具 | UUID、时间戳、密码生成 |
| 趣味实验室 | 随机选择、打乱行、随机数字、骰子、倒放文本、Emoji 装饰、Commit 文案、变量名灵感、假日志、程序员借口 |
| 在线小卡 | 随机一言、诗词一句、天气小卡、IP 信息小卡 |

## 2.0 Beta Notes

这一版主要是一次比较大的体验更新：

- 将主 UI 改造成 VS2022 深色工作台布局。
- 新增工具搜索和可折叠工具浏览器。
- 新增“趣味实验室”分组及多种离线小工具。
- 新增联网趣味工具和联网增强模式。
- URL 工具升级为编码、解码、拆解一体。
- 新增端口占用查询。
- 优化正则超时、文本 diff、DNS/公网 IP 超时等稳定性问题。
- 修复动态参数清空后显示值和执行值不同步的问题。

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

发布 Windows x64 自包含单文件：

```powershell
dotnet publish 小工具集合/小工具集合.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:Version=2.0.0-beta `
  -p:InformationalVersion=2.0-beta
```

## Project Structure

```text
小工具集合/
  Models/                 工具定义、请求和返回模型
  Services/
    ToolCatalog.cs        工具目录和参数元数据
    ToolProcessor.cs      工具调度入口
    ToolProcessors/       按功能拆分的 partial 工具处理器
  ViewModels/             主窗口状态、命令和偏好协调
  MainWindow.xaml         VS2022 风格主界面
  MainWindow.xaml.cs      动态参数控件和窗口交互
```

## Privacy

本工具不保存输入内容、密钥或执行历史。  
需要联网的工具会把当前输入发送到对应公开接口；如果不希望发送内容，请保持工具为本地模式。

## License

MIT License. See [LICENSE](LICENSE).

## Author

Made by **LCMasterSpark**.
