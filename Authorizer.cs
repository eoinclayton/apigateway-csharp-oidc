using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AwsDotnetCsharp
{
    public class Authorizer
    {
        private APIGatewayCustomAuthorizerRequest _request;
        private ClaimsPrincipal _claimsPrincipal;
        private string _token;
        private string _oidcIssuer;
        
        public async Task<APIGatewayCustomAuthorizerResponse> Authorize(APIGatewayCustomAuthorizerRequest request, ILambdaContext context)
        {
            try
            {
                _request = request;
                _token = request.AuthorizationToken;
                _oidcIssuer = Environment.GetEnvironmentVariable("OIDC_ISSUER");

                _claimsPrincipal = await AuthorizeViaOidc();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in {nameof(Authorize)}: {ex}");
                throw;
            }

            if (_claimsPrincipal?.Identity == null)
            {
                Console.WriteLine("The token is NOT authorized");
                throw new Exception(HttpStatusCode.Unauthorized.ToString()); // this is the recommended way to invoke a 401 response
            }

            Console.WriteLine("The token is authorized");
            
            return GenerateAllowResponse();
        }

        private async Task<ClaimsPrincipal> AuthorizeViaOidc()
        {
            var oidcIssuerMetadata = $"{_oidcIssuer}/.well-known/openid-configuration";
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                oidcIssuerMetadata, 
                new OpenIdConnectConfigurationRetriever());
            var openIdConfig = await configurationManager.GetConfigurationAsync(CancellationToken.None);
            
            var validationParameters =
                new TokenValidationParameters
                {
                    ValidIssuer = openIdConfig.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = openIdConfig.SigningKeys
                };

            var handler = new JwtSecurityTokenHandler();

            try
            {
                return handler.ValidateToken(_token, validationParameters, out _);
            }
            catch (SecurityTokenExpiredException)
            {
                Console.WriteLine("The token is well formed but expired");
                return null;
            }
            catch (SecurityTokenInvalidSigningKeyException exception)
            {
                Console.WriteLine($"The signing key isn't valid: {exception}");
                return null;
            }
            catch  (ArgumentException exception)
            {
                Console.WriteLine($"The token is badly formed: {exception}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        private APIGatewayCustomAuthorizerResponse GenerateAllowResponse()
        {
            // Grab the claims you would like from the token here. They can be passed in to the lambda being invoked via the Context
            var userGuid = _claimsPrincipal?.FindFirst(claim => claim.Type == "userGuid")?.Value;
            var username = _claimsPrincipal?.FindFirst(claim => claim.Type == "username")?.Value;
            var userId = _claimsPrincipal?.FindFirst(claim => claim.Type == "userId")?.Value;
            
            return new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = userGuid,
                Context = new APIGatewayCustomAuthorizerContextOutput
                {
                    ["userGuid"] = userGuid,
                    ["username"] = username,
                    ["userId"] = userId
                },
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Version = "2012-10-17",
                    Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>() {
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Action = new HashSet<string> {"execute-api:Invoke"},
                            Effect = "Allow",
                            Resource = new HashSet<string> { _request.MethodArn}
                        }
                    }
                }
            };
        }
    }
}