using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;
using System.Web;

namespace AWS_S3_FileUpload.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadFileController : ControllerBase
    {

        private const string keyName = "Image3.png"; // File name in S3
        private const string filePath = "D:\\project\\Practice_Project\\AWS_S3_FileUpload\\AWS_S3_FileUpload\\Image3.png"; // Local file path

        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _accessKey;
        private readonly string _secretKey;
        
        public UploadFileController(IConfiguration configuration)
        {
            _bucketName = configuration.GetSection("S3Configuration:bucketName").Value;
            _accessKey = configuration.GetSection("S3Configuration:AccessKey").Value;
            _secretKey = configuration.GetSection("S3Configuration:SecretKey").Value;
            string region = configuration.GetSection("S3Configuration:bucketRegion").Value;
            var bucketRegion = RegionEndpoint.GetBySystemName(region);

            _s3Client = new AmazonS3Client(_accessKey, _secretKey, bucketRegion);
        }

        //Uplodaing file by PutObjectAsync method and response will be get by PutObjectResponse
        [HttpPost("UploadFile")]
        public async Task<string> UploadFile(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            var fileKey = $"{DateTime.UtcNow:dd/MM/yyyy}/{file.FileName}"; // ✅ Correct S3 path

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileKey,  // ✅ Correct S3 path
                InputStream = stream,  // ✅ Use stream instead of FilePath
                ContentType = file.ContentType
            };

            PutObjectResponse response = await _s3Client.PutObjectAsync(putRequest);

            return $"File uploaded successfully! \n{JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })}";
        }

        //Download file by Etag & also you can download by file key value
        [HttpPost("DownloadFileByETag")]
        public async Task<string> DownloadFileByETag(string eTagKey)
        {
            var localDirectory = "D:\\project\\Practice_Project\\AWS_S3_FileUpload\\AWS_S3_FileUpload\\DownloadFolder\\";
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    EtagToMatch = eTagKey,
                };

                using var response = await _s3Client.GetObjectAsync(request);

                // Ensure directory exists
                if (!Directory.Exists(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                }

                // Save the file locally
                string localFilePath = Path.Combine(localDirectory, Path.GetFileName(response.Key));
                await using var fileStream = System.IO.File.Create(localFilePath);
                await response.ResponseStream.CopyToAsync(fileStream);

                return $"File downloaded successfully! \n";
            }

            catch (AmazonS3Exception ex)
            {
                return $"S3 Error: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }



        //Uploading file by TransferUtility & UploadAsync() method, Retrive response from PutObjectResponse then must uploadfile by PutObjectAsync()
        [HttpPost("UploadFileByTransferUtilityUploadRequest")]
        public async Task UploadFileByTransferUtilityUploadRequest()
        {

            var fileTransferUtilityRequest = new TransferUtilityUploadRequest
            {
                BucketName = _bucketName,
                FilePath = filePath,
                StorageClass = S3StorageClass.StandardInfrequentAccess,
                //PartSize = 6291456, // 6 MB
                Key = "Image1.png", // File name in S3 bucket
               // CannedACL = S3CannedACL.PublicRead // Makes the file public
            };

            var fileTransferUtility = new TransferUtility(_s3Client);
            var res = fileTransferUtility.UploadAsync(fileTransferUtilityRequest);
            return;
        }

        //Here download file by iterating in a loop to find ETag
        [HttpPost("DownloadFile")]
        public async Task DownloadFile()
        {
            string ETag = "\"e39c54ab089c6ced28efe3864b92ab6c\"";
            var localDirectory = "D:\\project\\Practice_Project\\AWS_S3_FileUpload\\AWS_S3_FileUpload\\DownloadFolder\\";
        
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName
            };
            var listResponse = await _s3Client.ListObjectsV2Async(listRequest);

            // Step 2: Iterate through files and find the matching ETag
            foreach (var obj in listResponse.S3Objects)
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = obj.Key
                };

                var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest);

                if (metadataResponse.ETag == ETag) // Compare ETag
                {
                    Console.WriteLine($"✅ Found matching file: {obj.Key}");
                    var downloadRequest = new GetObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = obj.Key
                    };

                    var response = await _s3Client.GetObjectAsync(downloadRequest);
                    if (!Directory.Exists(localDirectory))
                    {
                        Directory.CreateDirectory(localDirectory);
                    }

                    // Save the file locally
                    string localFilePath = Path.Combine(localDirectory, Path.GetFileName(metadataRequest.Key));
                    using var fileStream = System.IO.File.Create(localFilePath);
                    await response.ResponseStream.CopyToAsync(fileStream);

                }
            }
        }
        [HttpPost("DeleteFileAsync")]
        public async Task<string> DeleteFileAsync(string fileKey)
        {
            string fileKey1 = HttpUtility.UrlDecode(fileKey); // ✅ Decode first

            //var request = new GetObjectRequest
            //{
            //    BucketName = _bucketName,
            //    //EtagToMatch = fileKey
            //    Key = fileKey1 // ✅ Use full file path
            //};
            var response = await _s3Client.DeleteObjectAsync(_bucketName, fileKey1);
            return $"File downloaded successfully! \n" +
                      $"URL: https://{_bucketName}.s3.amazonaws.com/{fileKey} \n" +
                      $"response: {response} \n";
        }


    }
}
