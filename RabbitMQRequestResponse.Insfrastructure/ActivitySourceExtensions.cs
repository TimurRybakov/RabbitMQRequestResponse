using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;

namespace RabbitMQRequestResponse.Insfrastructure;

public static class ActivitySourceExtensions
{
    private const string SendActivityName = "SEND";

    private const string ReceiveActivityName = "RECEIVE";

    public static Activity? StartSendActivity(this ActivitySource activitySource, IBasicProperties props, string? activityName = null)
    {
        var parentActivityContext = Activity.Current == null
            ? GetParentActivityContext(props)
            : default;

        var combinedName = activityName is null ? SendActivityName : $"{SendActivityName} {activityName}";

        var activity = activitySource.CreateActivity(combinedName, ActivityKind.Producer, parentActivityContext);
        if (activity == null)
            return null;

        if (props.IsCorrelationIdPresent())
            activity.AddTag("CorrelationId", props.CorrelationId);

        activity.Start();

        if (activity.Id != null)
        {
            props.Headers ??= new Dictionary<string, object?>();
            props.Headers["activity.id"] = activity.Id;
        }

        return activity;
    }

    public static Activity? StartReceiveActivity(this ActivitySource activitySource, IReadOnlyBasicProperties props, string? activityName = null)
    {
        var parentActivityContext = GetParentActivityContext(props, true);

        var combinedName = activityName is null ? ReceiveActivityName : $"{ReceiveActivityName} {activityName}";

        var activity = activitySource.CreateActivity(combinedName, ActivityKind.Consumer, parentActivityContext);
        if (activity == null)
            return null;

        if (props.IsCorrelationIdPresent())
            activity.AddTag("CorrelationId", props.CorrelationId);

        activity.Start();

        return activity;
    }

    private static ActivityContext GetParentActivityContext(IReadOnlyBasicProperties props, bool isRemote = false)
    {        
        if (props.Headers?.TryGetValue("activity.id", out var headerValue) == true)
        {
            if (headerValue is byte[] bytes)
            {
                var activityId = Encoding.UTF8.GetString(bytes);
                if (ActivityContext.TryParse(activityId, null, out var activityContext))
                {
                    return isRemote && Activity.Current == null
                        ? new ActivityContext(activityContext.TraceId, activityContext.SpanId, activityContext.TraceFlags, activityContext.TraceState, isRemote)
                        : activityContext;
                }
            }
        }

        return default;
    }
}
