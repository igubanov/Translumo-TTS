using System.Text.Json;
using System.Text.Json.Serialization;

namespace Translumo.TTS.Engines.MultiVoiceTTS;

public class MultiVoiceConfiguration
{
    #region JSON schema
    public string RegexCharacterName { get; init; }
    public VoiceConfig[] Voices { get; init; }

    public class VoiceConfig
    {
        public TTSEngines Engine { get; init; }
        public string VoiceName { get; init; }
        public string[] CharacterNames { get; init; }
    }
    #endregion

    private Dictionary<string, EngineAndVoice> _characterConfigs = new();

    /// <summary>
    /// Returns configured voice for character name or first from config (when name is not fill or doesnt have configuration)
    /// </summary>
    /// <param name="name">Character name or null</param>
    /// <returns>Record with engine tts enum value and voice</returns>
    public EngineAndVoice GetVoiceForCharacter(string? name = null)
    {
        if (_characterConfigs.TryGetValue(name, out var config))
        {
            return config;
        }

        return _characterConfigs.Values.First();
    }

    public IEnumerable<TTSEngines> GetEngines() => Voices.Select(x => x.Engine).Distinct();

    public static MultiVoiceConfiguration Load(string filepath)
    {
        using var fileStream = File.OpenText(filepath);
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }, PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<MultiVoiceConfiguration>(fileStream.BaseStream, options);
        config.FillCharactersDictionary();

        return config;
    }

    private void FillCharactersDictionary()
    {
        _characterConfigs = Voices.Select(x => (Engine: new EngineAndVoice(x.Engine, x.VoiceName), x.CharacterNames))
            .SelectMany(x => x.CharacterNames.Select(charName => (charName, x.Engine)))
            .ToDictionary(x => x.charName, x => x.Engine);

        if (_characterConfigs.Count == 0)
        {
            throw new InvalidDataException("Config should contains one or more voice configurations.");
        }
    }

    public sealed record EngineAndVoice(TTSEngines Engine, string Voice);
}