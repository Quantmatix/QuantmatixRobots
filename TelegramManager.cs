using Quantmatix.Shared;
using Quantmatix.Shared.DAO;
using Quantmatix.Shared.Diagnostic;
using Quantmatix.Shared.Enums;
using Quantmatix.Shared.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = Quantmatix.Shared.Entity.Auth.User;

namespace Quantmatix.Robots;

public class TelegramManager(SharedDI sharedDI, TelegramClient telegramClient, QMLogger logger, WebsiteDBUnit websiteDBUnit) {
    /// <summary>
    /// Starts listening for messages
    /// </summary>
    public void Start() {
        telegramClient.Client.OnMessage += OnMessage;
        telegramClient.Client.OnUpdate += OnUpdate;
        telegramClient.Client.OnError += OnError;
        //telegramClient.Client.OnApiResponseReceived += OnApiResponseReceived;
        //telegramClient.Client.OnMakingApiRequest += OnMakingApiRequest;
    }
    
    /// <summary>
    /// Stops working
    /// </summary>
    public void Stop() {
        //Detach all events
        telegramClient.Client.OnMessage -= OnMessage;
        telegramClient.Client.OnUpdate -= OnUpdate;
        telegramClient.Client.OnError -= OnError;
        //telegramClient.Client.OnApiResponseReceived -= OnApiResponseReceived;
        //telegramClient.Client.OnMakingApiRequest -= OnMakingApiRequest;
    }

    //private ValueTask OnMakingApiRequest(ITelegramBotClient botClient, ApiRequestEventArgs args, CancellationToken cancellationToken) {
    //    throw new NotImplementedException();
    //}

    //private ValueTask OnApiResponseReceived(ITelegramBotClient botClient, ApiResponseEventArgs args, CancellationToken cancellationToken) {
    //    throw new NotImplementedException();
    //}

    private async Task OnError(Exception exception, HandleErrorSource source) {
        await logger.LogError(exception, $"Telegram error - {exception.Message}, source: {source}");
    }

    private async Task OnUpdate(Update update) {
        if (update is { CallbackQuery: { } query}) {
            await telegramClient.Client.AnswerCallbackQuery(query.Id, $"You picked {query.Data}");

            //Process the item clicks
            var parts = query.Data!.Split("_");
            switch (parts[0]) {
                case "subscribe":
                    await HandleSubscribe(update, int.Parse(parts[1]), (PeriodEnum)int.Parse(parts[2]));
                    break;
                case "unsubscribe":
                    await HandleUnsubscribe(update, int.Parse(parts[1]), (PeriodEnum)int.Parse(parts[2]));
                    break;
                default:
                    throw new Exception($"Unhandled callback:{parts[0]}");
            }

            await telegramClient.Client.SendMessage(query.Message!.Chat, $"User {query.From} clicked on {query.Data}");
        } else {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Event handler when a new message is retrieved from Telegram
    /// </summary>
    /// <param name="message"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    private async Task OnMessage(Message message, UpdateType type) {
#if DEBUG
        Console.WriteLine($"Telegram > {message.Date}:{message.From}:{message.Text}, Id:{message.Id}, MessageId:{message.MessageId}, MessageThreadId:{message.MessageThreadId}");
#endif
        if (message.Text != null && message.Text.StartsWith("/")) {
            var text = message.Text!;
            if (text.StartsWith("/start")) {
                //The token is the part after the start
                await CheckStart(message, text);
            } else if (text.Equals("/subscribe_ticker")) {
                await SubscribeTicker(message);
            } else if (text.Equals("/unsubscribe_ticker")) {
                await UnsubscribeTicker(message);
            } else {
                await logger.LogWarning($"Unsupported message:{text}");
            }
        }
    }

    /// <summary>
    /// Gets the user Id from the message information
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<User> GetUserFromMessageDetail(Update update) {
        var telegramId = update.Message?.Contact?.UserId;
        if (telegramId == null) {
            throw new Exception("No telegram id found");
        }

        var user = await websiteDBUnit.UserDAO.GetByTelegramId(telegramId.Value.ToString());
        if (user == null) {
            throw new Exception($"No telegram user found by id:{user}");
        }

        return user;
    }

    private async Task CheckStart(Message message, string text) {
        var telegramId = message.From?.Id;
        if (telegramId != null) {
            var parts = text.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            switch (parts.Length) {
                case 1:
                    //Just an empty start command, do nothing
                    break;
                case 2:
                    //2nd param should be a one time user link token
                    var token = parts[1];

                    var userApiKey = await websiteDBUnit.UserApiKeyDAO.GetByApiKey(token);
                    if (userApiKey == null) {
                        await logger.LogWarning($"Someone is trying to login with a token code that doesn't exist:{token}");
                    } else if (userApiKey.ExpiryDate != null && userApiKey.ExpiryDate < DateTime.UtcNow) {
                        //This token is expired, they can't use it
                        await logger.LogWarning($"Someone is trying to login with an expired token code:{userApiKey.ApiKey}");
                    } else if (userApiKey.MaxUses != null && userApiKey.MaxUses <= userApiKey.UseCount) {
                        //This token is expired, they can't use it
                        await logger.LogWarning($"Someone is trying to login with an token code they've used too many times:{userApiKey.ApiKey}");
                    } else {
                        //The token is valid, track it was used
                        await websiteDBUnit.UserApiKeyDAO.TrackUseById(userApiKey.Id);
                        var user = (await websiteDBUnit.UserDAO.GetById(userApiKey.UserId))!;

                        //Track this user links with this WhatsApp if we don't have it already
                        await websiteDBUnit.UserDAO.UpdateTelegramIdById(userApiKey.UserId, telegramId.ToString());
                        await logger.LogWarning($"User:{user.Id} : {user.Email} has linked their telegramId:{telegramId}");

                        await telegramClient.Client.SendMessage(message.Chat.Id, $"Hi {user.FirstName} {user.LastName} - you're now connected with your Quantmatix account - let's get to the Signals!");
                    }

                    break;
                default:
                    await logger.LogWarning($"Unsupported /start message:{text}");
                    break;
            }
        }
    }

    private InlineKeyboardButton CreateCallbackForTickerAndPeriod(string action, int tickerId, string ticker, PeriodEnum period) {
        var button = InlineKeyboardButton.WithCallbackData(
            $"{ticker} / {EnumUtils.GetAbbreviatedNameAttribute(period)}",
            $"{action}_{tickerId}_{(int)period}"
        );

        button.Style = KeyboardButtonStyle.Danger;

        return button;
    }

    private InlineKeyboardMarkup CreateKeyboardMapForTickers(string action) {
        //Construct a 2D array of InlineKeyboardButtons
        return new InlineKeyboardMarkup([
            [
                CreateCallbackForTickerAndPeriod(action, 13944, "ETH-USD", PeriodEnum.Hourly),
                CreateCallbackForTickerAndPeriod(action, 13944, "ETH-USD", PeriodEnum.FourHourly),
                CreateCallbackForTickerAndPeriod(action, 13944, "ETH-USD", PeriodEnum.Daily)
            ],
            [
                CreateCallbackForTickerAndPeriod(action, 13945, "BTC-USD", PeriodEnum.Hourly),
                CreateCallbackForTickerAndPeriod(action, 13945, "BTC-USD", PeriodEnum.FourHourly),
                CreateCallbackForTickerAndPeriod(action, 13945, "BTC-USD", PeriodEnum.Daily)
            ]
        ]);
    }

    private async Task SubscribeTicker(Message message) { 
        //Send the message with options to subscribe
        var optionMessage = await telegramClient.Client.SendMessage(
            chatId: message.From!.Id,
            text: "Select which instrument would you like to subscribe",
            replyMarkup: CreateKeyboardMapForTickers("subscribe")
        );
        Console.WriteLine($"Sent > Subscribe message:{optionMessage.Id}");
    }

    private async Task UnsubscribeTicker(Message message) { 
        //Send the message with options to subscribe
        var optionMessage = await telegramClient.Client.SendMessage(
            chatId: message.From!.Id,
            text: "Select which instrument would you like to unsubscribe",
            replyMarkup: CreateKeyboardMapForTickers("unsubscribe")
        );
        Console.WriteLine($"Sent > Unsubscribe message:{optionMessage.Id}");
    }

    private async Task HandleUnsubscribe(Update update, int tickerId, PeriodEnum period) {
        var user = await GetUserFromMessageDetail(update);

        await websiteDBUnit.UserTickerWatchDAO.UpdateTelegram(user.Id, tickerId, period, false);
    }

    private async Task HandleSubscribe(Update update, int tickerId, PeriodEnum period) {
        var user = await GetUserFromMessageDetail(update);

        await websiteDBUnit.UserTickerWatchDAO.UpdateTelegram(user.Id, tickerId, period, true);
    }
}