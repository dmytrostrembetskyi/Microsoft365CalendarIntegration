using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models;

namespace CalendarListenerApi;

[ApiController]
[Route("api/[controller]")]
public class CalendarEventsController(CalendarEventService service, ILogger<CalendarEventsController> logger)
    : ControllerBase
{
    [HttpPost("createEvent")]
    public async Task<IActionResult> CreateEvent([FromBody] EventDto eventDto)
    {
        try
        {
            var @event = new Event
            {
                Subject = eventDto.Title,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = eventDto.Description
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = eventDto.StartUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "UTC"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = eventDto.EndUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "UTC"
                }
            };
            await service.CreateEventAsync(eventDto.UserEmail, @event);
            return Ok("Event created! \n");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to create event: {ex.Message} \n");
        }
    }

    [HttpPost("eventListener")]
    public async Task<IActionResult> Post()
    {
        await service.ListSubscriptionsAsync();

        if (Request.Query.ContainsKey("validationToken"))
        {
            var token = Request.Query["validationToken"];
            return Content(token, "text/plain");
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        try
        {
            var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("value", out var notifications))
                foreach (var notification in notifications.EnumerateArray())
                {
                    var changeType = notification.GetProperty("changeType").GetString();
                    var resource = notification.GetProperty("resource").GetString();
                    var parts = resource.Split('/');
                    if (parts.Length >= 4 && parts[0].Equals("users", StringComparison.OrdinalIgnoreCase))
                    {
                        var user = parts[1];
                        var eventId = parts[3];

                        if (changeType == "deleted")
                            logger.LogInformation("Event deleted for user: {User}", user);
                        else if (changeType == "created")
                            try
                            {
                                var eventDetails = await service.GetEventDetailsAsync(user, eventId);
                                if (eventDetails != null)
                                {
                                    var description = eventDetails.BodyPreview ?? "(no description)";
                                    logger.LogInformation("Event created for user: {User}\n  Subject: {Subject}\n  Start: {Start}\n  End: {End}\n  Description: {Description}",
                                        user, eventDetails.Subject, eventDetails.Start?.DateTime, eventDetails.End?.DateTime, description);
                                }
                            }
                            catch
                            {
                                logger.LogInformation("Event created for user: {User} (details not found)", user);
                            }
                        else if (changeType == "updated")
                            try
                            {
                                var eventDetails = await service.GetEventDetailsAsync(user, eventId);
                                if (eventDetails != null)
                                {
                                    var description = eventDetails.BodyPreview ?? "(no description)";
                                    logger.LogInformation("Event updated for user: {User}\n  Subject: {Subject}\n  Start: {Start}\n  End: {End}\n  Description: {Description}",
                                        user, eventDetails.Subject, eventDetails.Start?.DateTime, eventDetails.End?.DateTime, description);
                                }
                            }
                            catch
                            {
                                logger.LogInformation("Event updated for user: {User} (details not found, possibly deleted)", user);
                            }
                    }
                }
            else
                logger.LogInformation("No notifications found in payload.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse notification: {Message}", ex.Message);
        }

        return Ok();
    }


    [HttpPost("createSubscription")]
    public async Task<IActionResult> Create()
    {
        try
        {
            await service.CreateSubscriptionAsync();
            return Ok("Subscription created. \n");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to create subscription: {ex.Message} \n");
        }
    }
}