using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TokenGenerator;

public partial class TokenGenerator
{
    private readonly ILogger<TokenGenerator> _logger;
    private readonly string _secret;
    private readonly string _appSecret;
    private readonly string _directLineUri;

    public TokenGenerator(ILogger<TokenGenerator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _secret = configuration.GetValue<string>("secret");
        _appSecret = configuration.GetValue<string>("appSecret");
        _directLineUri = configuration.GetValue<string>("directLineUri");
    }

    [Function("TokenGenerator")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        try
        {
            ValidateConfiguration();
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex.Message);
            return new BadRequestObjectResult(ex.Message);
        }

        using HttpClient client = new();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secret);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response = client.PostAsync(_directLineUri, null).Result;
        string responseContent = response.Content.ReadAsStringAsync().Result;
        var payload = JsonSerializer.Deserialize<DirectLinePayload>(responseContent);
        return new OkObjectResult(GenerateToken(payload));

    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_secret))
        {

            throw new ArgumentException("The 'secret' configuration value is not set.");
        }

        if (string.IsNullOrEmpty(_appSecret))
        {
            throw new ArgumentException("The 'appSecret' configuration value is not set.");
        }

        if (string.IsNullOrEmpty(_directLineUri))
        {
            throw new ArgumentException("The 'directLineUri' configuration value is not set.");
        }
    }

    private string GenerateToken(DirectLinePayload payload, int expireMinutes = 200)
    {
        var symmetricKey = Encoding.UTF8.GetBytes(_appSecret);
        var tokenHandler = new JwtSecurityTokenHandler();
        var now = DateTime.UtcNow;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
            new Claim("userId", Guid.NewGuid().ToString()),
            new Claim("userName", "you"),
            new Claim("connectorToken", payload.token),
            ]),
            Expires = now.AddMinutes(expireMinutes),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(symmetricKey), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
