using NodaTime;
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

namespace Quantmatix.Robots;

public class TelegramManager(SharedDI sharedDI, TelegramClient telegramClient, QMLogger logger, WebsiteDBUnit websiteDBUnit) {
    //TODO Get the user id from Telegram linked with our id somehow....
    private const int userId = 1;

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
                    await HandleSubscribe(int.Parse(parts[1]), (PeriodEnum)int.Parse(parts[2]));
                    break;
                case "unsubscribe":
                    await HandleUnsubscribe(int.Parse(parts[1]), (PeriodEnum)int.Parse(parts[2]));
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
        if (message.Text != null) {
            switch (message.Text.ToLower()) {
                case "/subscribe_ticker":
                    await SubscribeTicker(message);
                    break;
                case "/unsubscribe_ticker":
                    await UnsubscribeTicker(message);
                    break;
            }
        }
    }

    private InlineKeyboardButton CreateCallbackForTickerAndPeriod(string action, int tickerId, string ticker, PeriodEnum period) {
        return InlineKeyboardButton.WithCallbackData($"{ticker} / {EnumUtils.GetAbbreviatedNameAttribute(period)}", $"{action}_{tickerId}_{(int)period}");
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

    private async Task HandleUnsubscribe(int tickerId, PeriodEnum period) {
        await websiteDBUnit.UserTickerWatchDAO.UpdateTelegram(userId, tickerId, period, false);
    }

    private async Task HandleSubscribe(int tickerId, PeriodEnum period) {
        await websiteDBUnit.UserTickerWatchDAO.UpdateTelegram(userId, tickerId, period, true);
    }
}