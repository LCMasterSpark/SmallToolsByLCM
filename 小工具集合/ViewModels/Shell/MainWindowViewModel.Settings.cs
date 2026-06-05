// 文件作用：承载偏好保存、翻译设置保存和跨工具参数注入逻辑。
using System.Collections.Generic;
using 小工具集合.Models;
using 小工具集合.Services;

namespace 小工具集合.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void SavePreferences()
    {
        _preferenceService.Save(_preferences);
    }

    private void SaveTranslationSettings()
    {
        _translationSettingsService.Save(_translationSettings);
    }

    private void SetTranslationString(string currentValue, string newValue, Action<string> apply)
    {
        if (currentValue == newValue)
        {
            return;
        }

        apply(newValue);
        SaveTranslationSettings();
        OnPropertyChanged();
    }

    private void InjectTranslationSettings(Dictionary<string, string> parameters)
    {
        parameters["translationDefaultProvider"] = _translationSettings.DefaultProvider;
        parameters["libreTranslateEndpoint"] = _translationSettings.LibreTranslateEndpoint;
        parameters["libreTranslateApiKey"] = _translationSettings.LibreTranslateApiKey;
        parameters["azureEndpoint"] = _translationSettings.AzureEndpoint;
        parameters["azureRegion"] = _translationSettings.AzureRegion;
        parameters["azureKey"] = _translationSettings.AzureKey;
        parameters["deepLApiUrl"] = _translationSettings.DeepLApiUrl;
        parameters["deepLApiKey"] = _translationSettings.DeepLApiKey;
        parameters["googleApiKey"] = _translationSettings.GoogleApiKey;
        parameters["baiduAppId"] = _translationSettings.BaiduAppId;
        parameters["baiduSecret"] = _translationSettings.BaiduSecret;
        parameters["youdaoAppKey"] = _translationSettings.YoudaoAppKey;
        parameters["youdaoAppSecret"] = _translationSettings.YoudaoAppSecret;
        parameters["openAiBaseUrl"] = _translationSettings.OpenAiBaseUrl;
        parameters["openAiApiKey"] = _translationSettings.OpenAiApiKey;
        parameters["openAiModel"] = _translationSettings.OpenAiModel;
        parameters["ollamaEndpoint"] = _translationSettings.OllamaEndpoint;
        parameters["ollamaModel"] = _translationSettings.OllamaModel;
        parameters["translationUseGlossary"] = _translationSettings.UseGlossary ? "true" : "false";
        parameters["translationGlossary"] = _translationSettings.Glossary;
    }

}
