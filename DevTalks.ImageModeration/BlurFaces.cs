using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DevTalks.ImageModeration
{
    public class BlurFaces
    {
        IAmazonS3 S3Client { get; }

        public BlurFaces()
        {
            this.S3Client = new AmazonS3Client();
        }

        public BlurFaces(IAmazonS3 _s3Client)
        {
            this.S3Client = _s3Client;
        }

        public async Task<State> Run(State state, ILambdaContext context)
        {
            if (state.Faces.Count > 0)
            {
                using (Stream inputStream = await GetS3FileStream(state.Bucket, state.ImageKey))
                using (Stream outputStream = new MemoryStream())
                //using (FileStream fileStream = new FileStream("out.jpeg", FileMode.Create))
                {
                    var image = Image.Load(inputStream);
                    var smile = Image.Load("smile.png");

                    foreach (var face in state.Faces)
                    {
                        if (state.AgeLimit <= 0 || face.AgeRange.Low > state.AgeLimit)
                            continue;

                        var rectangle = new Rectangle(
                            (int)(image.Width * face.BoundingBox.Left),
                            (int)(image.Height * face.BoundingBox.Top),
                            (int)(image.Width * face.BoundingBox.Width),
                            (int)(image.Height * face.BoundingBox.Height)
                        );

                        if (!state.ShouldSmile)
                            image.Mutate(x => x.BoxBlur(20, rectangle));
                        else
                        {
                            smile.Mutate(x => x.Resize(Math.Max(rectangle.Height, rectangle.Width), Math.Max(rectangle.Height, rectangle.Width)));
                            image.Mutate(x => x.DrawImage(smile, 1, new Point(rectangle.Left, rectangle.Top)));
                        }
                    }

                    //image.SaveAsJpeg(fileStream);
                    image.Save(outputStream, ImageFormats.Jpeg);

                    await PutS3FileStream(state.Bucket, "processed_" + state.ImageKey, outputStream);

                    state.ProcessedImageURL = GetURL(state.Bucket, "processed_" + state.ImageKey, 10);
                }
            }

            return state;
        }

        public async Task<Stream> GetS3FileStream(string bucket, string key)
        {
            var response = await this.S3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = key
            });

            return response.ResponseStream;
        }

        public async Task<bool> PutS3FileStream(string bucket, string key, Stream stream)
        {
            var response = await this.S3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream
            });

            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        private string GetURL(string bucket, string key, int days)
        {
            string url = this.S3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Expires = DateTime.UtcNow.AddDays(days)
            });

            return url;
        }
    }
}
