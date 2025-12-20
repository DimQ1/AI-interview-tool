namespace SystemAudioAnalyzer.Services
{
    public class AppSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "google/gemini-2.0-flash-001";
        public string AnalysisPrompt { get; set; } = "You are developer on interview. Analyze the text. Extract questions asked by the speaker. For each question, provide a short answer 3-5 sentences in English and then the same answer in Russian. Return the result in JSON format: { \"questions\": [\"q1\", \"q2\"], \"answers\": [\"a1\", \"a2\"] } where a1 corresponds to q1.";
        public string TranslationPrompt { get; set; } = "Translate the following text to Russian.";
    }
}
