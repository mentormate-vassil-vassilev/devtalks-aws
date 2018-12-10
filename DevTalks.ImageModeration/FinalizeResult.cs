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
    public class FinalizeResult
    {
        IAmazonS3 S3Client { get; }

        public FinalizeResult()
        {
            this.S3Client = new AmazonS3Client();
        }

        public FinalizeResult(IAmazonS3 _s3Client)
        {
            this.S3Client = _s3Client;
        }

        public State PrepareResult(State state, ILambdaContext context)
        {
            state.Faces = null;
            return state;
        }

        public void CleanUp(State state, ILambdaContext context)
        {
            var response = this.S3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = state.Bucket,
                Key = state.ImageKey
            });
        }
    }
}
