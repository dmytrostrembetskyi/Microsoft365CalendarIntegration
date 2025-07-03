using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

public class CalendarEventService
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<CalendarEventService> _logger;
    private readonly string _tenantId;
    private readonly string _notificationUrl;
    private readonly string _resource;


    public CalendarEventService(IConfiguration configuration, ILogger<CalendarEventService> logger)
    {
        _clientId = configuration["AzureAd:ClientId"];
        _tenantId = configuration["AzureAd:TenantId"];
        _clientSecret = configuration["AzureAd:ClientSecret"];
        _notificationUrl = configuration["ListeningUrl"];
        _resource = configuration["Email"];
        _logger = logger;
    }

    public async Task CreateSubscriptionAsync()
    {
        var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
        var graphClient = new GraphServiceClient(credential);

        var subscription = new Subscription
        {
            ChangeType = "created,updated,deleted",
            NotificationUrl = $"{_notificationUrl}/api/calendarevents/eventListener",
            Resource = $"users/{_resource}/events",
            ExpirationDateTime = DateTime.UtcNow.AddHours(1)
        };

        await graphClient.Subscriptions.PostAsync(subscription);
    }

    public async Task<Event?> GetEventDetailsAsync(string userId, string eventId)
    {
        var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
        var graphClient = new GraphServiceClient(credential);
        return await graphClient.Users[userId].Events[eventId].GetAsync();
    }

    public async Task ListSubscriptionsAsync()
    {
        var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
        var graphClient = new GraphServiceClient(credential);

        var subscriptions = await graphClient.Subscriptions.GetAsync();

        _logger.LogInformation("Current Subscriptions:");
        foreach (var sub in subscriptions.Value) _logger.LogInformation("Subscription Id: {SubscriptionId}, Resource: {Resource}, Expiration: {ExpirationDateTime}", sub.Id, sub.Resource, sub.ExpirationDateTime);
    }

    public async Task CreateEventAsync(string userEmail, Event @event)
    {
        var credential = new ClientSecretCredential(_tenantId, _clientId, _clientSecret);
        var graphClient = new GraphServiceClient(credential);
        await graphClient.Users[userEmail].Events.PostAsync(@event);
    }
}