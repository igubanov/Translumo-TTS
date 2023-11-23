using NAudio.Wave;

namespace Translumo.TTS.Engines;

public class YandexTTSEngine : ITTSEngine
{
    private readonly string _langCode;
    private string _voice;
    private CancellationTokenSource _tokenSource;
    private readonly DirectSoundOut _naudioPlayer;
    private readonly string _emotion = "neutral"; // neutral|good|evil
    private const float _speed = 1.0f;
    private const string _format = "mp3"; // ogg | mp3 | wav
    private const string _key = "1"; // yandex doesnt validate key, its can have any not empty value

    public YandexTTSEngine(string langCode)
    {
        _langCode = langCode;
        _voice = GetVoices().First();
        _naudioPlayer = new DirectSoundOut();
        _tokenSource = new CancellationTokenSource();
    }

    public void SpeechText(string text)
    {
        _tokenSource.Cancel();
        _tokenSource = new CancellationTokenSource();
        var currentToken = _tokenSource.Token;
        Task.Run(() => SpeechTextInternalAsync(text, currentToken));
    }

    private async Task SpeechTextInternalAsync(string text, CancellationToken token)
    {
        using var stream = await RequestAudioAsync(text, token);
        using var audioFileReader = new Mp3FileReader(stream);

        _naudioPlayer.Stop();
        _naudioPlayer.Init(audioFileReader);
        _naudioPlayer.Play();

        var duration = Convert.ToInt32(Math.Round(audioFileReader.TotalTime.TotalMilliseconds, MidpointRounding.ToPositiveInfinity));
        Task.Delay(duration).Wait(token);
    }

    private async Task<Stream> RequestAudioAsync(string text, CancellationToken token)
    {
        var httpClient = new HttpClient();
        var httpResponse = await httpClient.GetAsync(BuildUrl(text), token).ConfigureAwait(false);
        if (httpResponse.IsSuccessStatusCode)
        {
            return await httpResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        }

        var error = await httpResponse.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException($"Failed to get sound: '{httpResponse.StatusCode}, {error}'");
    }

    private string BuildUrl(string text)
    {
        var encodedText = Uri.EscapeDataString(text);
        return $"https://tts.voicetech.yandex.net/generate?text={encodedText}&lang={_langCode}&key={_key}&speaker={_voice}&format={_format}&speed={_speed:#.##}&emotion={_emotion}&quality=hi";
    }

    public void SetVoice(string voice) =>
    _voice = GetVoices().FirstOrDefault(x => x.Equals(voice, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException($"Voice '{voice}' doesnt support '{_langCode}' language ");

    public string[] GetVoices() =>
        SupportedVoices().GetValueOrDefault(_langCode)
        ?? throw new InvalidDataException($"{nameof(YandexTTSEngine)} doesnt support '{_langCode}' language");

    public void Dispose()
    {
        _tokenSource.Cancel();
        _naudioPlayer.Stop();
        _naudioPlayer.Dispose();
    }

    private static Dictionary<string, string[]> SupportedVoices()
    {
        var listOfVoices = new[] {
            // dictor dialog voices: zahar, ermil, filipp,|  jane, omazh, alena
            "zahar", "ermil",  "dude", "jane", "omazh", "oksana",
            // additional voices
            "alyss", "erkanyavas", "ermilov", "kolya", "kostya", "levitan", "nastya", "nick", "sasha", "silaerkan", "smoky", "tanya", "voicesearch", "zhenya", "zombie"
        };

        return new Dictionary<string, string[]>()
        {
            // dictor dialog voices: zahar, ermil, filipp,|  jane, omazh, alena
            { "ru-RU", listOfVoices},
            { "en-US", listOfVoices},
            { "tr-TR", listOfVoices},
        };
    }
}
