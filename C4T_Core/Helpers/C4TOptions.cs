namespace C4T_Core.Helpers
{
    public class C4TOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public bool UseStreamlabs { get; set; }
        public string? AppDataPath { get; set; }
        public string? ChatChannel { get; set; }
        public bool FollowReact { get; set; }
        public bool RaidReact { get; set; }
        public bool DailyMessageReact { get; set; }
        public bool UseTextToSpeech { get; set; }
        public string? SpeechKey { get; set; }
        public string SpeechRegion { get { return "eastus"; } }
        public string? SpeechSynthesisVoiceName { get; set; }

    }
}
