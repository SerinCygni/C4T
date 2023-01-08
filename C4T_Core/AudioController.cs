using C4T_Core.Helpers;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4T_Core
{
    internal class AudioController
    {
        private SpeechConfig speechConfig;
        private SpeechSynthesizer synthesizer;
        private CancellationTokenSource cancelTokenSource;
        private Queue<string> ssmlQueue;
        public AudioController(C4TOptions options)
        {
            speechConfig = SpeechConfig.FromSubscription(options.SpeechKey, options.SpeechRegion);
            speechConfig.SpeechSynthesisVoiceName = options.SpeechSynthesisVoiceName;
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff48Khz16BitMonoPcm);
            synthesizer = new SpeechSynthesizer(speechConfig);
            cancelTokenSource = new();
            ssmlQueue = new();
            var token = cancelTokenSource.Token;
            Task.Factory.StartNew(
                () => 
                {
                    while(!token.IsCancellationRequested)
                    {
                        AudioLoop();
                    }
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private async void AudioLoop()
        {
            string? ssmlToPlay = "";
            if(ssmlQueue.TryDequeue(out ssmlToPlay))
            {
                await synthesizer.SpeakSsmlAsync(ssmlToPlay);
            }
        }

        public void AddToQueue(string ssml)
        {
            ssmlQueue.Enqueue(ssml);
        }
    }
}
