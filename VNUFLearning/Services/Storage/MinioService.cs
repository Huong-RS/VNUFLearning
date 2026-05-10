using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace VNUFLearning.Services.Storage
{
    public class MinioService : IMinioService
    {
        private readonly IMinioClient _client;
        private readonly MinioSettings _settings;

        public MinioService(
            IMinioClient client,
            IOptions<MinioSettings> options)
        {
            _client = client;
            _settings = options.Value;
        }

        public async Task<StorageUploadResult> UploadAsync(
            IFormFile file,
            string folder,
            string[] allowedExtensions,
            long maxSizeBytes = 100 * 1024 * 1024)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File rỗng.");

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(ext))
                throw new Exception("File không hợp lệ.");

            var objectName =
                $"{folder}/{Guid.NewGuid()}{ext}";

            using var stream = file.OpenReadStream();

            await _client.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType));

            return new StorageUploadResult
            {
                BucketName = _settings.BucketName,
                ObjectName = objectName,
                Url = GetFileUrl(objectName),
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                Size = file.Length
            };
        }

        public async Task DeleteAsync(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return;

            await _client.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(objectName));
        }

        public string GetFileUrl(string objectName)
        {
            return
                $"{_settings.PublicEndpoint}/{_settings.BucketName}/{objectName}";
        }
    }
}