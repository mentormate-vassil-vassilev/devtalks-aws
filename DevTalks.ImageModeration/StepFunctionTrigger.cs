using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace DevTalks.ImageModeration
{
    public class StepFunctionTrigger
    {
        IAmazonStepFunctions SfClient { get; }
        IAmazonS3 S3Client { get; }
        string StepFunctionArn { get; }

        public StepFunctionTrigger()
        {
            this.SfClient = new AmazonStepFunctionsClient();
            this.S3Client = new AmazonS3Client();
            this.StepFunctionArn = Environment.GetEnvironmentVariable("StepFunctionArn");
        }

        public StepFunctionTrigger(IAmazonStepFunctions _sfClient, int _ageLimit, string _stepFunctionArn)
        {
            this.SfClient = _sfClient;
            this.StepFunctionArn = _stepFunctionArn;
        }

        public async Task<APIGatewayProxyResponse> Run(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            if (request.Path.EndsWith("/file"))
                return await Upload(request, lambdaContext);
            else if (request.Path.EndsWith("/check") && request.QueryStringParameters.ContainsKey("executionId"))
                return await Check(request, lambdaContext);

            return new APIGatewayProxyResponse
            {
                StatusCode = 404,
                IsBase64Encoded = false,
                Body = JsonConvert.SerializeObject(new { Message = "Not Found" })
            };
        }

        public async Task<APIGatewayProxyResponse> Upload(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            Console.WriteLine(JsonConvert.SerializeObject(request));

            var definition = new { ImageName = "", Content = "", AgeLimit = 18, ShouldSmile = false };
            var payload = JsonConvert.DeserializeAnonymousType(request.Body, definition);

            await S3Upload(payload.Content, payload.ImageName);

            var state = new State {
                Bucket = "devtalks-image-moderation",
                ImageKey = payload.ImageName,
                AgeLimit = payload.AgeLimit,
                ShouldSmile = payload.ShouldSmile
            };

            var startResponse = await this.SfClient.StartExecutionAsync(new StartExecutionRequest
            {
                Input = JsonConvert.SerializeObject(state),
                Name = Guid.NewGuid().ToString(),
                StateMachineArn = this.StepFunctionArn
            });

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                IsBase64Encoded = false,
                Body = JsonConvert.SerializeObject(new { executionId = startResponse.ExecutionArn }),
                Headers = new Dictionary<string, string>
                {
                    { "X-Requested-With", "*" },
                    { "Access-Control-Allow-Headers", "Content-Type,X-Amz-Date,Authorization,X-Api-Key,x-requested-with" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "Access-Control-Allow-Methods", "POST,GET,OPTIONS" }
                }
            };
        }

        public async Task<bool> S3Upload(string body, string imageName)
        {
            var bytes = Convert.FromBase64String(body);
            var stream = new MemoryStream(bytes);

            var putRequest = new PutObjectRequest
            {
                BucketName = "devtalks-image-moderation",
                Key = imageName,
                InputStream = stream
            };

            var response = await this.S3Client.PutObjectAsync(putRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        public async Task<APIGatewayProxyResponse> Check(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            string executionId = request.QueryStringParameters["executionId"];

            var executionResponse = await this.SfClient.DescribeExecutionAsync(new DescribeExecutionRequest
            {
                ExecutionArn = executionId
            });

            Console.WriteLine("Execution status of the StepFunction: " + executionResponse.Status.Value);
            Console.WriteLine("Output of the StepFunction: " + executionResponse.Output);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                IsBase64Encoded = false,
                Body = JsonConvert.SerializeObject(new
                {
                    Status = executionResponse.Status.Value,
                    Output = executionResponse.Status == ExecutionStatus.SUCCEEDED ? executionResponse.Output : ""
                }),
                Headers = new Dictionary<string, string>
                {
                    { "X-Requested-With", "*" },
                    { "Access-Control-Allow-Headers", "Content-Type,X-Amz-Date,Authorization,X-Api-Key,x-requested-with" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "Access-Control-Allow-Methods", "POST,GET,OPTIONS" }
                }
            };
        }
    }
}
