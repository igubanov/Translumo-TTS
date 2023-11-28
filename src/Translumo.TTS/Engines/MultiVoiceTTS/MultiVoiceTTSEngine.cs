using System.Linq;
using System.Text.RegularExpressions;
using Translumo.Utils.Extensions;
using Windows.Devices.AllJoyn;
using Windows.Foundation.Metadata;

namespace Translumo.TTS.Engines.MultiVoiceTTS;

public class MultiVoiceTTSEngine : ITTSEngine
{
    private string[] _availableConfigs = Array.Empty<string>();
    private MultiVoiceConfiguration _config;
    private const string CONFIG_DIRECTORY = "multi-voice-configs";
    private const string CONFIG_EXTENSION = ".json";
    private readonly string _langCode;
    private readonly TtsFactory _ttsFactory;
    private readonly Dictionary<TTSEngines, ITTSEngine> Engines = new();

    public MultiVoiceTTSEngine(string langCode, TtsFactory ttsFactory)
    {
        _langCode = langCode;
        _ttsFactory = ttsFactory;
        Init();
    }

    private void Init() => SetVoice(GetVoices().First());


    public void Dispose()
    {
    }

    public string[] GetVoices()
    {
        if (_availableConfigs.Length != 0)
        {
            return _availableConfigs;
        }

        _availableConfigs = GetConfigFileNames();
        return _availableConfigs;
    }

    private string[] GetConfigFileNames()
    {
        var configDirectory = GetConfigDirectory();

        var configNames = Directory.GetFiles(configDirectory, $"*{CONFIG_EXTENSION}")
            .Select(Path.GetFileName)
            .Select(x => x.Replace(Path.GetExtension(x), ""))
            .ToArray();

        if (configNames.Length == 0)
        {
            throw new InvalidDataException($"Directory '{configDirectory}' doesnt contain any *.json configs.");
        }

        return configNames;
    }

    private static string GetConfigDirectory() => Path.Combine(Directory.GetCurrentDirectory(), CONFIG_DIRECTORY);

    public void SetVoice(string voice)
    {
        try
        {
            LoadConfig(voice);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Cant set voice '{voice}', error: {ex.Message}", ex);
        }
    }

    private void LoadConfig(string voice)
    {
        var filepath = Path.Combine(GetConfigDirectory(), voice + CONFIG_EXTENSION);
        _config = MultiVoiceConfiguration.Load(filepath);

        var engines = _config.GetEngines().ToArray();
        var engineToAdd = engines.Except(Engines.Keys);
        var engineToRemove = Engines.Keys.Except(engines);

        engineToAdd.ForEach(x => Engines.Add(x, _ttsFactory.CreateTtsEngine(_langCode, x)));
        engineToAdd.ForEach(RemoveEngine);
    }

    private void RemoveEngine(TTSEngines engine)
    {
        var removedEngine = Engines[engine];
        Engines.Remove(engine);
        removedEngine.Dispose();
    }

    public void SpeechText(string text)
    {
        var regex = new Regex(_config.RegexCharacterName);
        var match = regex.Match(text);
        var characterName = string.Empty;
        if (match.Success)
        {
            text = regex.Replace(text, "");
            characterName = match.Groups[1].Value;
        }

        var characterConfig = _config.GetVoiceForCharacter(characterName);
        var engine = Engines[characterConfig.Engine];
        engine.SetVoice(characterConfig.Voice);
        engine.SpeechText(text);
    }
}
