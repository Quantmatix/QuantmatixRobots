using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quantmatix.Shared;
using Quantmatix.Shared.Enums;

namespace Quantmatix.Robots.Settings; 

/// <summary>
/// All settings combined, app.settings, config, key vaults, etc.
/// </summary>
public class AppSettings(SecretClient secretClient, ILogger<AppSettings> logger, SendGridSettings sendGrid) {
    /// <summary>
    /// A reference to the host running the app
    /// </summary>
    public IHost Host { get; private set; } = null!;

    public string SendGridApiKey { get; private set; } = null!;
    public string SendGridValidationApiKey { get; private set; } = null!;
    public string GrokApiKey { get; private set; } = null!;
    public string OpenAIApiKey { get; private set; } = null!;
    public string ClaudeApiKey { get; private set; } = null!;
    public string CryptoComApiKey { get; private set; } = null!;
    public string CryptoComSecret { get; private set; } = null!;
    public string SlackBot { get; private set; } = null!;
    public string TelegramToken { get; private set; } = null!;
    
    /// <summary>
    /// SendGrid settings
    /// </summary>
    public SendGridSettings SendGrid { get; } = sendGrid;

    /// <summary>
    /// Tracks the environment this code is running in
    /// </summary>
    public EnvironmentEnum Environment { get; set; } = EnumUtils.GetEnvironmentEnumByCodeBuild();

    /// <summary>
    /// Loads up extra config from the key vault
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public async Task InitialiseSecrets(IHost host) {
        Host = host;

        //Now load other settings
        SendGridApiKey = (await GetSecretValue("SendGridApiKey"))!;
        SendGridValidationApiKey = (await GetSecretValue("SendGridValidationApiKey"))!;
        GrokApiKey = (await GetSecretValue("GrokApiKey"))!;
        OpenAIApiKey = (await GetSecretValue("OpenAIApiKey"))!;
        ClaudeApiKey = (await GetSecretValue("ClaudeApiKey"))!;
        CryptoComApiKey = (await GetSecretValue("CryptoComApiKey"))!;
        CryptoComSecret = (await GetSecretValue("CryptoComSecret"))!;
        SlackBot = (await GetSecretValue("SlackBot"))!;
        TelegramToken = (await GetSecretValue("TelegramToken"))!;
    }

    private async Task<string?> GetSecretValue(string secretName, bool swallowException = false) {
        try {
            var secret = await secretClient.GetSecretAsync(secretName);
            if (secret?.Value.Value == null) {
                //No value...swallow or not?
                if (swallowException) {
                    logger.LogInformation($"{secretName} secret was not found in the KeyVault - it is optional");
                    return null;
                }

                throw new Exception($"{secretName} secret was not found in the KeyVault - it is required");
            }

            return secret.Value.Value;
        } catch (Exception ex) {
            //No value...swallow or not?
            if (swallowException) {
                logger.LogInformation(ex, $"{secretName} secret was not found in the KeyVault - it is optional");
                return null;
            }

            throw new Exception($"{secretName} secret was not found in the KeyVault - it is required");
        }
    }
}