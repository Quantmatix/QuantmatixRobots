using Azure.Core.Serialization;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using OpenTelemetry;
using Quantmatix.Robots;
using Quantmatix.Robots.Settings;
using Quantmatix.Shared;
using Quantmatix.Shared.Diagnostic;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder => {
        builder.Services.AddLogging();

        //Configure JSON how we want
        builder.Services.Configure<WorkerOptions>(workerOptions => {
            workerOptions.Serializer = new NewtonsoftJsonObjectSerializer(JsonUtils.DefaultSettings);
            JsonConvert.DefaultSettings = () => JsonUtils.DefaultSettings;
        });
    })
    .ConfigureAppConfiguration((_, builder) => {
#if DEBUG
        builder.AddJsonFile("appsettings.debug.json", false);
        builder.AddJsonFile("local.settings.json", true, true); //File is optional because we deploy a DEBUG to Azure where the file won't be.  In local dev, you should have this file, in fact you will need it to store the KeyVault
#elif QA
        builder.AddJsonFile("appsettings.qa.json", false);
#else
        builder.AddJsonFile("appsettings.json", false);
#endif
    })
    .ConfigureServices((context, services) => {
        //Setup DI
        SharedDI.RegisterServices(context.Configuration, services);
        
        //For this project, the main DB is the study DB, and the live one is a special connection we use to copy processed study data into the main DB where the website uses this data
        var studyDBUnit = SharedDI.RegisterStudyDBUnit(context.Configuration, services);
        SharedDI.RegisterWebsiteDBUnit(context.Configuration, services);
        SharedDI.RegisterDataDBUnit(context.Configuration, services);
        SharedDI.RegisterAnalysisDBUnit(context.Configuration, services);

        services.AddSingleton(new QMLoggerDBUnit(studyDBUnit.ConnectionString));
        services.AddSingleton<QMLogger>();

        //We use a default Http client as well, so register that
        services.AddSingleton(new HttpClient());

        //Configure settings
        services.AddSingleton<AppSettings>();
        services.AddSingleton(context.Configuration.GetSection("SendGrid").Get<SendGridSettings>()!);
        
        services.AddSingleton<TelegramManager>();
    })
    .Build();

//Now services are ready, we have to initialize some
var appSettings = host.Services.GetService<AppSettings>()!;
await appSettings.InitialiseSecrets(host);

//Setup helpers/services
var sharedDI = host.Services.GetService<SharedDI>()!;
sharedDI.SlackClient.Init(appSettings.SlackBot);
sharedDI.SendGridClient.Init(appSettings.SendGridApiKey);
sharedDI.SendGridClient.ConfigureValidationClient(appSettings.SendGridValidationApiKey);
sharedDI.OpenAIClient.Init(appSettings.OpenAIApiKey);
sharedDI.ClaudeClient.Init(appSettings.ClaudeApiKey);
sharedDI.GrokClient.Init(appSettings.GrokApiKey);
sharedDI.CryptoComClient.Init();
sharedDI.CryptoComExchangeClient.Init(appSettings.CryptoComApiKey, appSettings.CryptoComSecret);
sharedDI.TelegramClient.Init(appSettings.TelegramToken);

//Run!
var appTask = host.RunAsync();

//Start the Telegram robot
var telegramManager = host.Services.GetService<TelegramManager>()!;
telegramManager.Start();

//Now run the functions!
await appTask;