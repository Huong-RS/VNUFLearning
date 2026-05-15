using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace VNUFLearning.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<GeminiGradeResult> GradeEssayAsync(
            string questionContent,
            string correctAnswer,
            string studentAnswer,
            string? rubric = null)
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new GeminiGradeResult
                {
                    Percent = 0,
                    SemanticSimilarity = 0,
                    Comment = "Chua cau hinh Gemini API Key.",
                    Advice = "Vui long kiem tra appsettings.json.",
                    IsFallback = true
                };
            }

            var prompt = BuildPrompt(questionContent, correctAnswer, studentAnswer, rubric);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var responseText = await SendPromptAsync(url, prompt);
                    var rawText = ExtractCandidateText(responseText);
                    var result = TryParseGradeResult(rawText);

                    if (result != null)
                    {
                        return result;
                    }
                }
                catch
                {
                    // Retry once, then use fallback so the submission flow never crashes.
                }
            }

            return BuildFallbackResult(correctAnswer, studentAnswer);
        }

        private static string BuildPrompt(
            string questionContent,
            string correctAnswer,
            string studentAnswer,
            string? rubric)
        {
            return $@"
Bạn là bộ phân tích ngữ nghĩa cho hệ thống chấm tự luận. Bạn KHÔNG được quyết định điểm cuối cùng.
Backend sẽ tự tính điểm. Bạn chỉ trả về dữ liệu phân tích theo JSON.

DỮ LIỆU ĐẦU VÀO:
- Câu hỏi: ""{EscapeForPrompt(questionContent)}""
- Đáp án mẫu của giảng viên: ""{EscapeForPrompt(correctAnswer)}""
- Rubric/gợi ý chấm của giảng viên nếu có: ""{EscapeForPrompt(rubric ?? "")}""
- Bài làm sinh viên: ""{EscapeForPrompt(studentAnswer)}""

NGUYÊN TẮC BẮT BUỘC:
1. Chỉ so sánh bài làm sinh viên với câu hỏi, đáp án mẫu và rubric được cung cấp.
2. Không tự thêm kiến thức, ví dụ, yêu cầu hoặc tiêu chí ngoài dữ liệu đầu vào.
3. Không trả về điểm số cuối cùng, không dùng trường score, mark, grade, finalScore.
4. Tự tách đáp án mẫu/rubric thành các ý chính cần có, keyword quan trọng, trọng số phần trăm và mức độ quan trọng.
5. Tổng weightPercent của mainIdeas phải bằng 100. Nếu rubric đã có phần trăm thì ưu tiên theo rubric; nếu không có thì tự phân bổ theo mức độ quan trọng trong đáp án mẫu.
6. Đánh giá đúng theo ngữ nghĩa: chấp nhận cách diễn đạt khác nếu cùng bản chất; không chấm đúng khi bài làm chỉ nhắc keyword nhưng sai ý.
7. percent là phần trăm chính xác tổng thể từ 0 đến 100 dựa trên các ý đúng/thiếu, không phải điểm.
8. Nếu bài làm trống hoặc không liên quan, percent = 0.
9. Chỉ trả JSON hợp lệ. Không markdown, không ```json, không giải thích ngoài JSON.

JSON SCHEMA BẮT BUỘC:
{{
  ""mainIdeas"": [
    {{
      ""id"": ""I1"",
      ""idea"": ""ý chính cần có"",
      ""keywords"": [""keyword 1"", ""keyword 2""],
      ""weightPercent"": 0,
      ""importance"": ""high|medium|low""
    }}
  ],
  ""matchedIdeas"": [
    {{
      ""id"": ""I1"",
      ""studentEvidence"": ""phần trả lời của sinh viên khớp ý này"",
      ""semanticSimilarity"": 0
    }}
  ],
  ""missingIdeas"": [
    {{
      ""id"": ""I2"",
      ""idea"": ""ý còn thiếu""
    }}
  ],
  ""semanticSimilarity"": 0,
  ""percent"": 0,
  ""comment"": ""nhận xét ngắn gọn về ý đúng và ý thiếu"",
  ""advice"": ""một câu góp ý ôn tập""
}}";
        }

        private async Task<string> SendPromptAsync(string url, string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.0,
                    topP = 0.1,
                    topK = 1,
                    responseMimeType = "application/json"
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var response = await _httpClient.PostAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"));

            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Gemini API error {(int)response.StatusCode}: {responseText}");
            }

            return responseText;
        }

        private static string ExtractCandidateText(string responseText)
        {
            var obj = JObject.Parse(responseText);
            return obj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "";
        }

        private static GeminiGradeResult? TryParseGradeResult(string rawText)
        {
            rawText = rawText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return null;
            }

            var token = JToken.Parse(rawText);
            if (token.Type != JTokenType.Object)
            {
                return null;
            }

            var obj = (JObject)token;
            if (obj["score"] != null || obj["finalScore"] != null || obj["grade"] != null || obj["mark"] != null)
            {
                return null;
            }

            var result = obj.ToObject<GeminiGradeResult>();
            if (result == null)
            {
                return null;
            }

            result.Percent = ClampPercent(result.Percent);
            result.SemanticSimilarity = ClampPercent(result.SemanticSimilarity);
            result.MainIdeas ??= new List<GeminiMainIdea>();
            result.MatchedIdeas ??= new List<GeminiMatchedIdea>();
            result.MissingIdeas ??= new List<GeminiMissingIdea>();
            result.Comment ??= "";
            result.Advice ??= "";

            return result;
        }

        private static GeminiGradeResult BuildFallbackResult(string correctAnswer, string studentAnswer)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer))
            {
                return new GeminiGradeResult
                {
                    Percent = 0,
                    SemanticSimilarity = 0,
                    Comment = "Sinh viên chưa nhập câu trả lời tự luận.",
                    Advice = "Cần trả lời các ý chính trong đáp án mẫu.",
                    IsFallback = true
                };
            }

            var percent = EstimateKeywordPercent(correctAnswer, studentAnswer);

            return new GeminiGradeResult
            {
                Percent = percent,
                SemanticSimilarity = percent,
                Comment = "Gemini không trả về JSON hợp lệ. Hệ thống dùng fallback so khớp keyword để tránh lỗi.",
                Advice = "Giảng viên nên kiểm tra lại bài tự luận này nếu cần độ chính xác cao.",
                IsFallback = true
            };
        }

        private static double EstimateKeywordPercent(string correctAnswer, string studentAnswer)
        {
            var expectedWords = NormalizeWords(correctAnswer)
                .Where(w => w.Length >= 4)
                .Distinct()
                .ToList();

            if (!expectedWords.Any())
            {
                return 0;
            }

            var actualWords = NormalizeWords(studentAnswer).ToHashSet();
            var matched = expectedWords.Count(actualWords.Contains);

            return Math.Round(ClampPercent(matched * 100.0 / expectedWords.Count), 2);
        }

        private static IEnumerable<string> NormalizeWords(string value)
        {
            var chars = value.ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                .ToArray();

            return new string(chars)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static double ClampPercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return Math.Max(0, Math.Min(100, value));
        }

        private static string EscapeForPrompt(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }
    }

    public class GeminiGradeResult
    {
        [JsonProperty("mainIdeas")]
        public List<GeminiMainIdea> MainIdeas { get; set; } = new();

        [JsonProperty("matchedIdeas")]
        public List<GeminiMatchedIdea> MatchedIdeas { get; set; } = new();

        [JsonProperty("missingIdeas")]
        public List<GeminiMissingIdea> MissingIdeas { get; set; } = new();

        [JsonProperty("semanticSimilarity")]
        public double SemanticSimilarity { get; set; }

        [JsonProperty("percent")]
        public double Percent { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("advice")]
        public string Advice { get; set; } = "";

        [JsonIgnore]
        public bool IsFallback { get; set; }
    }

    public class GeminiMainIdea
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("idea")]
        public string Idea { get; set; } = "";

        [JsonProperty("keywords")]
        public List<string> Keywords { get; set; } = new();

        [JsonProperty("weightPercent")]
        public double WeightPercent { get; set; }

        [JsonProperty("importance")]
        public string Importance { get; set; } = "";
    }

    public class GeminiMatchedIdea
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("studentEvidence")]
        public string StudentEvidence { get; set; } = "";

        [JsonProperty("semanticSimilarity")]
        public double SemanticSimilarity { get; set; }
    }

    public class GeminiMissingIdea
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("idea")]
        public string Idea { get; set; } = "";
    }
}
