using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsDotnetCsharp
{
    public class Handler
    {
       public APIGatewayProxyResponse Hello(APIGatewayProxyRequest request, ILambdaContext contexts)
       {
           Console.WriteLine("Request: " + JsonConvert.SerializeObject(request));
           return GenerateOkResponse("Hello World!");
       }

       private APIGatewayProxyResponse GenerateOkResponse(string body)
       {
           var headers = new Dictionary<string, string>();
           headers.Add("Access-Control-Allow-Origin", "*");
           headers.Add("Access-Control-Allow-Headers", "Content-Type");
           headers.Add("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS");
           headers.Add("Access-Control-Allow-Credentials", "true");
         
           return new APIGatewayProxyResponse
           {
               StatusCode = (int)HttpStatusCode.OK,
               Headers = headers,
               Body = body
           };
       }
    }
}
