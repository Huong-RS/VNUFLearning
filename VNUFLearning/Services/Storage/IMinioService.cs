using Microsoft.AspNetCore.Http;

namespace VNUFLearning.Services.Storage
{
    public interface IMinioService
    {
        Task<StorageUploadResult> UploadAsync(
            IFormFile file,
            string folder,
            string[] allowedExtensions,
            long maxSizeBytes = 100 * 1024 * 1024);

        Task DeleteAsync(string objectName);

        string GetFileUrl(string objectName);
    }
}