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
Bạn là một Giảng viên đại học giàu kinh nghiệm, chuyên chấm thi tự luận.
Nhiệm vụ của bạn là đánh giá bài làm của sinh viên dựa trên đáp án chuẩn do hệ thống cung cấp.

THÔNG TIN BÀI LÀM:
- Câu hỏi: ""{questionContent}""
- Đáp án chuẩn (Các ý chính cần có): ""{correctAnswer}""
- Bài làm của sinh viên: ""{studentAnswer}""

QUY TẮC CHẤM ĐIỂM (BẮT BUỘC TUÂN THỦ):
1. SO KHỚP NGỮ NGHĨA (SEMANTIC MATCHING): KHÔNG yêu cầu sinh viên phải viết trùng khớp từng từ 100% với đáp án chuẩn. Nếu sinh viên sử dụng từ đồng nghĩa, diễn đạt theo cách khác nhưng BẢN CHẤT VÀ Ý NGHĨA VẪN ĐÚNG thì BẮT BUỘC phải cho điểm ý đó.
2. PHÂN TÍCH Ý: Hãy chia 'Đáp án chuẩn' thành các ý nhỏ. Xem bài của sinh viên có bao nhiêu ý khớp với các ý nhỏ đó. 
3. TÍNH ĐIỂM LINH HOẠT: 
   - Có làm và đúng một phần nhỏ: 10% - 30%
   - Đúng ý chính nhưng thiếu chi tiết: 40% - 60%
   - Đúng đa số các ý quan trọng: 70% - 90%
   - Xuất sắc, hiểu đúng bản chất: 95% - 100%

HƯỚNG DẪN TRẢ VỀ:
Hãy trả về DUY NHẤT một chuỗi JSON hợp lệ theo đúng định dạng sau, tuyệt đối KHÔNG có markdown, KHÔNG có thẻ ```json:
{{
  ""percent"": <Số nguyên từ 0 đến 100, thể hiện % mức độ đúng>,
  ""score"": <Số thực từ 0.0 đến 10.0, quy đổi từ percent. Ví dụ: percent là 85 thì score là 8.5>,
  ""comment"": ""<Phân tích chi tiết: Ghi rõ sinh viên đã nói đúng được ý nào (khen ngợi), và chỉ ra cụ thể sinh viên diễn đạt sai hoặc thiếu ý nào so với đáp án gốc>"",
  ""advice"": ""<1 câu khuyên sinh viên nên ôn tập thêm phần nào>""
}}";

            var requestBody = new
            {
                contents = new[]
      {
        new { parts = new[] { new { text = prompt } } }
    },
                generationConfig = new
                {
                    temperature = 0.1, // Càng thấp, AI càng làm việc máy móc và công tâm (Tốt cho chấm thi)
                    responseMimeType = "application/json"
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
                // In thẳng lỗi thực tế của Google ra màn hình để bắt bệnh
                return new GeminiGradeResult
                {
                    Percent = 0,
                    Score = 0,
                    Comment = $"LỖI TỪ GOOGLE (Mã {(int)response.StatusCode}): {responseText}",
                    Advice = "Em hãy copy dòng lỗi ở trên gửi cho cô để cô bắt bệnh nhé!"
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