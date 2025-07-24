using Amazon.S3;
using Amazon.S3.Model;
using VurduGololdu.API.Helpers;

namespace VurduGololdu.API.Services
{
    public interface IS3Service
    {
        Task<string> UploadFileAsync(IFormFile file, string folder = "uploads");
        Task<bool> DeleteFileAsync(string fileUrl);
        Task<string> GetPresignedUrlAsync(string key, int expireHours = 1);
        bool IsValidImageFile(IFormFile file);
    }

    public class S3Service : IS3Service
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;
        private readonly string _bucketName;
        private readonly string _baseUrl;

        public S3Service(IConfiguration configuration)
        {
            _configuration = configuration;
            var s3Config = _configuration.GetSection("AwsS3");
            _bucketName = s3Config["BucketName"]!;
            _baseUrl = s3Config["BaseUrl"]!;

            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3Config["Region"])
            };

            _s3Client = new AmazonS3Client(s3Config["AccessKey"], s3Config["SecretKey"], config);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder = "uploads")
        {
            if (!IsValidImageFile(file))
                throw new ArgumentException("Geçersiz dosya formatı");

            var fileKey = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            // 🔧 EN GÜVENLİ ÇÖZÜM: Byte array + Manual disposal
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            // Stream'i AWS call'dan SONRA dispose etmek için değişken olarak tut
            MemoryStream? uploadStream = null;

            try
            {
                uploadStream = new MemoryStream(fileBytes);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey,
                    InputStream = uploadStream,
                    ContentType = file.ContentType,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                    CannedACL = S3CannedACL.PublicRead
                };

                var response = await _s3Client.PutObjectAsync(request);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    DebugConsole.Log($"✅ File uploaded successfully with public read access: {fileKey}");

                    // 🔧 KALICI ÇÖZÜM: Public URL döndür (AWS ayarları artık aktif)
                    var publicUrl = $"{_baseUrl}{fileKey}";
                    DebugConsole.Log($"🔗 Public URL: {publicUrl}");

                    // Fallback: Eğer public URL çalışmazsa presigned URL döndür
                    try
                    {
                        using var httpClient = new HttpClient();
                        var response2 = await httpClient.GetAsync(publicUrl);
                        if (response2.IsSuccessStatusCode)
                        {
                            DebugConsole.Log($"✅ Public URL verified: {publicUrl}");
                            return publicUrl; // 🎯 KALICI URL
                        }
                    }
                    catch
                    {
                        DebugConsole.Log($"⚠️ Public URL not accessible yet, using presigned URL");
                    }

                    // Fallback: Presigned URL (7 gün geçerli)
                    var presignedUrl = await GetPresignedUrlAsync(fileKey, 24 * 7); // 7 gün
                    DebugConsole.Log($"🔗 Fallback Presigned URL (7 days): {presignedUrl}");
                    return presignedUrl;
                }
            }
            catch (AmazonS3Exception ex) when (ex.Message.Contains("ACL"))
            {
                DebugConsole.Log($"⚠️ ACL not supported, trying without ACL: {fileKey}");

                // Yeni stream oluştur (eski dispose edilmiş olabilir)
                uploadStream?.Dispose();
                uploadStream = new MemoryStream(fileBytes);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey,
                    InputStream = uploadStream,
                    ContentType = file.ContentType,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                    // ACL olmadan dene - Bucket Policy ile public access sağlanacak
                };

                var response = await _s3Client.PutObjectAsync(request);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    DebugConsole.Log($"✅ File uploaded without ACL (using bucket policy): {fileKey}");

                    // 🔧 KALICI ÇÖZÜM: Public URL test et
                    var publicUrl = $"{_baseUrl}{fileKey}";

                    // Public URL'i test et
                    try
                    {
                        using var httpClient = new HttpClient();
                        var response2 = await httpClient.GetAsync(publicUrl);
                        if (response2.IsSuccessStatusCode)
                        {
                            DebugConsole.Log($"✅ Public URL verified (no ACL): {publicUrl}");
                            return publicUrl; // 🎯 KALICI URL
                        }
                    }
                    catch
                    {
                        DebugConsole.Log($"⚠️ Public URL not accessible, using presigned URL");
                    }

                    // Fallback: Presigned URL (7 gün geçerli)
                    var presignedUrl = await GetPresignedUrlAsync(fileKey, 24 * 7); // 7 gün
                    DebugConsole.Log($"🔗 Fallback Presigned URL (7 days): {presignedUrl}");
                    return presignedUrl;
                }
            }
            catch (AmazonS3Exception ex)
            {
                DebugConsole.Log($"🚨 S3 Error: {ex.ErrorCode} - {ex.Message}");
                throw new Exception($"S3 Upload Error: {ex.Message}");
            }
            finally
            {
                // 🔧 Stream'i sonunda dispose et
                uploadStream?.Dispose();
            }

            throw new Exception("Dosya yüklenirken hata oluştu");
        }

        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl) || !fileUrl.StartsWith(_baseUrl))
                    return false;

                var fileKey = fileUrl.Replace(_baseUrl, "");

                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey
                };

                var response = await _s3Client.DeleteObjectAsync(request);
                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetPresignedUrlAsync(string key, int expireHours = 1)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddHours(expireHours),
                Verb = HttpVerb.GET
            };

            return await _s3Client.GetPreSignedURLAsync(request);
        }

        // 🔧 Public URL'in çalışıp çalışmadığını test et
        public async Task<bool> TestPublicAccessAsync(string fileUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(fileUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            var maxSizeInBytes = _configuration.GetValue<int>("FileUpload:MaxFileSizeInMB") * 1024 * 1024;
            if (file.Length > maxSizeInBytes)
                return false;

            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>();
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return allowedExtensions?.Contains(extension) == true;
        }
    }
}