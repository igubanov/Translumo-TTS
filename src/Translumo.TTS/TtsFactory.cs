using Microsoft.Extensions.Logging;
using Translumo.Infrastructure.Language;
using Translumo.Infrastructure.Python;
using Translumo.TTS.Engines;
using Translumo.TTS.Engines.MultiVoiceTTS;
using Translumo.Utils.Extensions;

namespace Translumo.TTS
{
    public class TtsFactory
    {
        private readonly LanguageService _languageService;
        private readonly PythonEngineWrapper _pythonEngine;
        private readonly IObserverAvailableVoices _observerAvailableVoices;
        private readonly ILogger _logger;

        public TtsFactory(LanguageService languageService, PythonEngineWrapper pythonEngine, IObserverAvailableVoices observerAvailableVoices, ILogger<TtsFactory> logger)
        {
            _languageService = languageService;
            _pythonEngine = pythonEngine;
            _observerAvailableVoices = observerAvailableVoices;
            _logger = logger;
        }

        public ITTSEngine CreateTtsEngine(TtsConfiguration ttsConfiguration)
        {
            var ttsEngine = CreateTtsEngine(GetLangCode(ttsConfiguration), ttsConfiguration.TtsSystem);

            var voices = ttsEngine.GetVoices();
            _observerAvailableVoices.UpdateVoice(voices);
            return ttsEngine;
        }

        public ITTSEngine CreateTtsEngine(string langCode, TTSEngines engine) => engine switch
        {
            TTSEngines.None => new NoneTTSEngine(),
            TTSEngines.WindowsTTS => new WindowsTTSEngine(langCode),
            TTSEngines.SileroTTS => new SileroTTSEngine(_pythonEngine, langCode),
            TTSEngines.YandexTTS => new YandexTTSEngine(langCode),
            TTSEngines.MultiVoiceTTS => new MultiVoiceTTSEngine(langCode, this),
            _ => throw new NotSupportedException()
        };

        private string GetLangCode(TtsConfiguration ttsConfiguration) =>
            _languageService.GetLanguageDescriptor(ttsConfiguration.TtsLanguage).Code;
    }
}
