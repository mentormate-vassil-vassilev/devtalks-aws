using Amazon.Lambda.Core;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace DevTalks.ImageModeration
{
    public class ImageAnalysis
    {
        IAmazonRekognition RekognitionClient { get; }
        float MinConfidence = 70;
        List<string> SupportedImageTypes { get; } = new List<string> { ".jpg", ".jpeg" };

        public ImageAnalysis()
        {
            this.RekognitionClient = new AmazonRekognitionClient();
        }

        public State CheckImageType(State state, ILambdaContext context)
        {
            if (!SupportedImageTypes.Contains(Path.GetExtension(state.ImageKey)))
            {
                string errorMessage = $"Object {state.Bucket}:{state.ImageKey} is not a supported image type";
                Console.WriteLine(errorMessage);
                throw new NotSupportedException(errorMessage);
            }

            return state;
        }

        public async Task<State> Run(State state, ILambdaContext context)
        {
            var photo = new Image
            {
                S3Object = new Amazon.Rekognition.Model.S3Object
                {
                    Bucket = state.Bucket,
                    Name = state.ImageKey
                }
            };

            Console.WriteLine($"Analyzing image {state.Bucket}:{state.ImageKey}");
            var detectLabels = await this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                MinConfidence = MinConfidence,
                Image = photo
            });

            var tags = new Dictionary<string, string>();
            foreach (var label in detectLabels.Labels.Take(10))
            {
                Console.WriteLine($"\tFound Label {label.Name} with confidence {label.Confidence}");
                tags.Add(label.Name, label.Confidence.ToString());
            }

            // Now detect faces
            DetectFacesResponse detectFaces = await this.RekognitionClient.DetectFacesAsync(new DetectFacesRequest
            {
                Attributes = new List<string> { "ALL" },
                Image = photo
            });

            foreach (var face in detectFaces.FaceDetails)
            {
                Console.WriteLine($"\tFound face {face.BoundingBox.ToString()} with age {face.AgeRange.ToString()}");
            }

            var celebrityResult = await this.RekognitionClient.RecognizeCelebritiesAsync(new RecognizeCelebritiesRequest
            {
                Image = photo
            });

            if(celebrityResult.CelebrityFaces.Count > 0)
            {
                state.Celebrity = celebrityResult.CelebrityFaces.First();
            }

            state.Labels = tags;
            state.Faces = detectFaces.FaceDetails;

            return state;
        }
    }
}
