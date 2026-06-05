// 文件作用：读写翻译功能的统一配置，避免各翻译入口分散保存 API Key 和默认语言。
namespace 小工具集合.Services;

public sealed class TranslationSettingsService
{
    private readonly AppStateService _appStateService = new();

    public TranslationSettings Load()
    {
        return _appStateService.LoadTranslation();
    }

    public void Save(TranslationSettings settings)
    {
        _appStateService.SaveTranslation(settings);
    }
}
