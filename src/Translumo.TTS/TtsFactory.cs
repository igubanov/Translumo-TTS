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
        private readonly ILogger _logger;
        private readonly TaskScheduler _uiScheduler;
        private CancellationTokenSource _cancelationTokenSource;

        public TtsFactory(LanguageService languageService, PythonEngineWrapper pythonEngine, ILogger<TtsFactory> logger)
        {
            _languageService = languageService;
            _pythonEngine = pythonEngine;
            _logger = logger;
            _uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            _cancelationTokenSource = new CancellationTokenSource();
        }

        public ITTSEngine CreateTtsEngine(TtsConfiguration ttsConfiguration)
        {
            var ttsEngine = CreateTtsEngine(GetLangCode(ttsConfiguration), ttsConfiguration.TtsSystem);

            var voices = ttsEngine.GetVoices();
            UpdateAvailableAndCurrentVoiceAsync(ttsConfiguration, voices).ConfigureAwait(false);
            return ttsEngine;
        }

        public ITTSEngine CreateTtsEngine(string langCode, TTSEngines engine)
        {
            return engine switch
            {
                TTSEngines.None => new NoneTTSEngine(),
                TTSEngines.WindowsTTS => new WindowsTTSEngine(langCode),
                TTSEngines.SileroTTS => new SileroTTSEngine(_pythonEngine, langCode),
                TTSEngines.YandexTTS => new YandexTTSEngine(langCode),
                TTSEngines.MultiVoiceTTS => new MultiVoiceTTSEngine(langCode, this),
                _ => throw new NotSupportedException()
            };
        }

        private string GetLangCode(TtsConfiguration ttsConfiguration) =>
            _languageService.GetLanguageDescriptor(ttsConfiguration.TtsLanguage).Code;

        private async Task UpdateAvailableAndCurrentVoiceAsync(TtsConfiguration ttsConfiguration, string[] voices)
        {
            var currentVoice = voices.Contains(ttsConfiguration.CurrentVoice)
                ? ttsConfiguration.CurrentVoice
                : voices.First();

            _cancelationTokenSource.Cancel();
            _cancelationTokenSource = new CancellationTokenSource();

            await RunOnUIAsync(() =>
                {
                    ttsConfiguration.AvailableVoices.Clear();
                    voices.ForEach(ttsConfiguration.AvailableVoices.Add);
                }, _cancelationTokenSource.Token);

            ttsConfiguration.CurrentVoice = currentVoice;
        }

        private Task RunOnUIAsync(Action action, CancellationToken token)
        {
            var taskFactory = new TaskFactory(
                token,
                TaskCreationOptions.DenyChildAttach,
                TaskContinuationOptions.None,
                _uiScheduler);

            return taskFactory.StartNew(action);
        }
    }
}
