using System;
using System.Globalization;
using System.IO;

namespace HourSync;

internal static class LocalizationService
{
    private const string LanguageSettingKey = "uiLanguage";
    public const string SystemLanguageValue = "system";
    private static readonly Lazy<Microsoft.Windows.ApplicationModel.Resources.ResourceManager> ResourceManager = new(CreateResourceManager);

    public static void ApplySavedLanguage()
    {
        ApplyLanguage(GetSavedLanguage());
    }

    public static string GetSavedLanguage()
    {
        var appSettings = FileMgr.LoadAppSettings();
        if (appSettings.TryGetValue(LanguageSettingKey, out var value))
        {
            var language = value?.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                return language;
            }
        }

        return SystemLanguageValue;
    }

    public static void SetLanguagePreference(string languageCode)
    {
        var appSettings = FileMgr.LoadAppSettings();
        appSettings[LanguageSettingKey] = languageCode;
        FileMgr.SaveAppSettings(appSettings);
    }

    public static void ApplyLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || languageCode == SystemLanguageValue)
        {
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            }
            catch (Exception ex)
            {
                FileMgr.Log("Error resetting language to system default: " + ex.Message);
                return;
            }
            return;
        }

        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageCode;
    }

    public static string GetString(string resourceKey)
    {
        try
        {
            var resourceContext = ResourceManager.Value.CreateResourceContext();
            var savedLanguage = GetSavedLanguage();
            if (savedLanguage != SystemLanguageValue)
            {
                resourceContext.QualifierValues["Language"] = savedLanguage;
            }

            var resourceMap = ResourceManager.Value.MainResourceMap.GetSubtree("Resources");
            // WinUI 3 converts dots to forward slashes for PRI resource paths
            var value = resourceMap.GetValue(resourceKey.Replace(".", "/"), resourceContext)?.ValueAsString;
            return string.IsNullOrWhiteSpace(value) ? resourceKey : value;
        }
        catch (Exception ex)
        {
            FileMgr.Log("Error fetching the key '" + resourceKey + "', returning resource key");
            FileMgr.Log("Error: " + ex.Message);
            return resourceKey;
        }
    }

    private static Microsoft.Windows.ApplicationModel.Resources.ResourceManager CreateResourceManager()
    {
        var priPath = Path.Combine(AppContext.BaseDirectory, "HourSync.pri");
        return File.Exists(priPath)
            ? new Microsoft.Windows.ApplicationModel.Resources.ResourceManager(priPath)
            : new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();
    }

    public static string PrepareStatement(string resourceKey, string toPrep, string YYY="", string ZZZ="", string AAA="", string BBB="", string CCC="")
    {
        try
        {
            var str = GetString(resourceKey);
            if (string.IsNullOrWhiteSpace(str))
            {
                return $"PreparedStatement({resourceKey}, {toPrep})";
            }
            var toReturn = str.Replace("XXX", toPrep);
            if (!string.IsNullOrEmpty(YYY))
            {
                toReturn = toReturn.Replace("YYY", YYY);
            }

            if (!string.IsNullOrEmpty(ZZZ))
            {
                toReturn = toReturn.Replace("ZZZ", ZZZ);
            }

            if (!string.IsNullOrEmpty(AAA))
            {
                toReturn = toReturn.Replace("AAA", AAA);
            }

            if (!string.IsNullOrEmpty(BBB))
            {
                toReturn = toReturn.Replace("BBB", BBB);
            }

            if (!string.IsNullOrEmpty(CCC))
            {
                toReturn = toReturn.Replace("CCC", CCC);
            }

            return toReturn;
        }
        catch (Exception ex)
        {
            FileMgr.Log("Error preparing statement for key '" + resourceKey + "', returning resource key");
            FileMgr.Log("Error: " + ex.Message);
            return $"PreparedStatement({resourceKey}, {toPrep})";
        }
    }

    public static string FormatNumber(string num, int places = 2)
    {
        var culture = GetActiveCulture();
        if (double.TryParse(num, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
        {
            return number.ToString("N" + places.ToString(), culture);
        }
        return num;
    }

    public static string FormatLocalizedDate(string input)
    {
        var culture = GetActiveCulture();

        if (!DateTime.TryParse(input, culture, DateTimeStyles.None, out DateTime dt))
        {
            string[] formats = { "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss" };
            if (!DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                if (!DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    return input;
                }
            }
        }

        // Localized "at" word
        var atWord = LocalizationService.GetString("GenericAt"); // e.g. "at" / "um"

        // Choose format based on culture
        string format;

        if (culture.TwoLetterISOLanguageName == "en")
        {
            // English → 12-hour with AM/PM
            format = $"dddd, MMMM d, yyyy '{atWord}' hh:mm:ss tt";
        }
        else
        {
            // Most other languages → 24-hour
            format = $"dddd, MMMM d, yyyy '{atWord}' HH:mm:ss";
        }

        return dt.ToString(format, culture);
    }

    private static CultureInfo GetActiveCulture()
    {
        var cultureCode = GetSavedLanguage();
        return cultureCode == SystemLanguageValue ? CultureInfo.CurrentCulture : new CultureInfo(cultureCode);
    }

    // Debug helper: log whether the key can be found via ResourceManager
    public static void DumpResourceDiagnostics(string resourceKey)
    {
        try
        {
            var resourceContext = ResourceManager.Value.CreateResourceContext();
            var resourceMap = ResourceManager.Value.MainResourceMap.GetSubtree("Resources");
            var value = resourceMap.GetValue(resourceKey.Replace(".", "/"), resourceContext)?.ValueAsString;
            FileMgr.Log($"Resource value for {resourceKey}: {value ?? "<null>"}");
        }
        catch (Exception ex)
        {
            FileMgr.Log("DumpResourceDiagnostics exception: " + ex);
        }
    }
}
