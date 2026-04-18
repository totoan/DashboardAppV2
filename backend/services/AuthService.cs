using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DashboardBackend.Services;

public class OAuthConfig
{
    public string client_id { get; set; } = "";
    public string client_secret { get; set; } = "";
}

public class AuthService
{
    private OAuthConfig? _config;

    private const string RedirectUri = "http://localhost:5000/";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Scope = "https://www.googleapis.com/auth/youtube.readonly";

    private const string TokenFile = "token.dat";

    private string? _accessToken;
    public string? AccessToken => _accessToken;

    public AuthService()
    {
        _config = LoadConfig();
    }

    // =============================
    // PUBLIC ENTRY POINT
    // =============================
    public async Task<bool> InitializeAsync()
    {
        var saved = LoadToken();

        if (saved != null)
        {
            try
            {
                TokenResponse loginTokens;

                if (saved != null && !string.IsNullOrEmpty(saved.refresh_token))
                
                {
                    try
                    {
                        loginTokens = await RefreshTokenAsync(saved.refresh_token);

                        if (string.IsNullOrEmpty(loginTokens.refresh_token))
                        {
                            loginTokens.refresh_token = saved.refresh_token;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        loginTokens = await LoginAsync();
                    }
                }
                else
                {
                    Debug.WriteLine("Refresh Token not found. Please log in...");
                    loginTokens = await LoginAsync();
                }

                _accessToken = loginTokens.access_token;
                SaveToken(loginTokens);

                return true;
            }
            catch
            {
                Debug.WriteLine("refresh failed → fall back to login");
            }
        }

        var tokens = await LoginAsync();
        _accessToken = tokens.access_token;

        SaveToken(tokens);
        return true;
    }

    public void SignOut()
    {
        if (File.Exists(TokenFile))
            File.Delete(TokenFile);
    }

    // =============================
    // LOGIN FLOW (PKCE)
    // =============================
    private async Task<TokenResponse> LoginAsync()
    {
        _config = LoadConfig();

        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        string authRequest =
            $"{AuthUrl}" +
            $"?client_id={_config.client_id}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scope)}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&access_type=offline";

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        Process.Start(new ProcessStartInfo
        {
            FileName = authRequest,
            UseShellExecute = true
        });

        var context = await listener.GetContextAsync();
        string? code = context.Request.QueryString["code"];

        if (string.IsNullOrEmpty(code))
            throw new Exception("Authorization code not found in redirect.");

        // respond to browser
        string response = "<html><body>You can close this window.</body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(response);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.OutputStream.Close();

        listener.Stop();

        return await ExchangeCodeAsync(code, codeVerifier);
    }

    private OAuthConfig LoadConfig()
    {
        string json = File.ReadAllText("oauth_config.json");

        var config = JsonSerializer.Deserialize<OAuthConfig>(json);

        if (config == null)
            throw new Exception("Failed to load OAuth config");

        return config;
    }

    private async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier)
    {
        if (_config == null)
            throw new Exception("[Exception] _config is null.");

        var values = new Dictionary<string, string>
        {
            { "client_id", _config.client_id },
            { "client_secret", _config.client_secret },
            { "code", code },
            { "code_verifier", codeVerifier },
            { "grant_type", "authorization_code" },
            { "redirect_uri", RedirectUri }
        };

        var client = new HttpClient();
        var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(values));

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<TokenResponse>(json);
        
        if (token == null)
            throw new Exception("Failed to deserialize token response");

        return token;
    }

    // =============================
    // REFRESH TOKEN
    // =============================
    private async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        if (_config == null)
            throw new Exception("[Exception] _config is null.");

        var values = new Dictionary<string, string>
        {
            { "client_id", _config.client_id },
            { "client_secret", _config.client_secret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        var client = new HttpClient();
        var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(values));

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<TokenResponse>(json);
        
        if (token == null)
            throw new Exception("Failed to deserialize token response");

        return token;
    }

    // =============================
    // TOKEN STORAGE (ENCRYPTED)
    // =============================
    private void SaveToken(TokenResponse token)
    {
        string json = JsonSerializer.Serialize(token);

        var data = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(TokenFile, encrypted);
    }

    private TokenResponse? LoadToken()
    {
        if (!File.Exists(TokenFile))
            return null;

        var encrypted = File.ReadAllBytes(TokenFile);
        var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);

        var json = Encoding.UTF8.GetString(decrypted);
        var token = JsonSerializer.Deserialize<TokenResponse>(json);
        
        if (token == null)
            throw new Exception("Failed to deserialize token response");

        return token;
    }

    // =============================
    // PKCE HELPERS
    // =============================
    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private string GenerateCodeChallenge(string verifier)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}

// =============================
// TOKEN MODEL
// =============================
public class TokenResponse
{
    public string? access_token { get; set; } = "";
    public int expires_in { get; set; }
    public string? refresh_token { get; set; } = "";
    public string? scope { get; set; } = "";
    public string? token_type { get; set; } = "";
}