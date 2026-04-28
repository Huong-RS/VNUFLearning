using Newtonsoft.Json;
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
            string studentAnswer)
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new GeminiGradeResult
                {
                    Percent = 0,
                    Score = 0,
                    Comment = "Chưa cấu hình Gemini API Key.",
                    Advice = "Vui lòng kiểm tra appsettings.json."
                };
            }

            var prompt = $@"
Bạn là giảng viên đang chấm bài tự luận cho sinh viên.

Yêu cầu:
- Đọc bài làm của sinh viên.
- So sánh với đáp án chuẩn.
- Tính mức độ đúng theo phần trăm.
- Quy đổi ra điểm thang 10.
- Nhận xét rõ ý đúng, ý thiếu.
- Góp ý ngắn gọn để sinh viên rút kinh nghiệm.

Câu hỏi:
{questionContent}

Đáp án chuẩn:
{correctAnswer}

Bài làm sinh viên:
{studentAnswer}

Chỉ trả về JSON đúng định dạng sau, không giải thích thêm bên ngoài:
{{
  ""percent"": 0,
  ""score"": 0,
  ""comment"": ""nhận xét"",
  ""advice"": ""góp ý""
}}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var response = await _httpClient.PostAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"));

            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var friendlyMessage = "Gemini AI hiện chưa chấm được bài này.";

                if ((int)response.StatusCode == 429 || responseText.Contains("RESOURCE_EXHAUSTED"))
                {
                    friendlyMessage = "Gemini API đã vượt quá giới hạn miễn phí. Vui lòng thử lại sau hoặc đổi API key khác.";
                }

                return new GeminiGradeResult
                {
                    Percent = 0,
                    Score = 0,
                    Comment = friendlyMessage,
                    Advice = "Bài tự luận đã được lưu, nhưng AI chưa thể phân tích do giới hạn API."
                };
            }

            dynamic? obj = JsonConvert.DeserializeObject(responseText);

            string rawText = obj?.candidates?[0]?.content?.parts?[0]?.text?.ToString() ?? "";

            rawText = rawText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            try
            {
                var result = JsonConvert.DeserializeObject<GeminiGradeResult>(rawText);

                return result ?? new GeminiGradeResult
                {
                    Percent = 0,
                    Score = 0,
                    Comment = "Không đọc được kết quả Gemini.",
                    Advice = rawText
                };
            }
            catch
            {
                return new GeminiGradeResult
                {
                    Percent = 0,
                    Score = 0,
                    Comment = "Gemini trả về không đúng JSON.",
                    Advice = rawText
                };
            }
        }
    }

    public class GeminiGradeResult
    {
        [JsonProperty("percent")]
        public double Percent { get; set; }

        [JsonProperty("score")]
        public double Score { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; } = "";

        [JsonProperty("advice")]
        public string Advice { get; set; } = "";
    }
}