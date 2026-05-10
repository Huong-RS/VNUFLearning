namespace VNUFLearning.Services.Storage
{
    public class StorageUploadResult
    {
        public string BucketName { get; set; }
        public string ObjectName { get; set; }
        public string Url { get; set; }
        public string OriginalFileName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
    }
}