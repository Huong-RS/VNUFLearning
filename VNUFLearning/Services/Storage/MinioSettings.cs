namespace VNUFLearning.Services.Storage
{
    public class MinioSettings
    {
        public string Endpoint { get; set; } = null!;

        public string AccessKey { get; set; } = null!;

        public string SecretKey { get; set; } = null!;

        public string BucketName { get; set; } = null!;

        public bool UseSSL { get; set; }

        public string PublicEndpoint { get; set; } = null!;
    }
}