using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;

namespace RabbitMQRequestResponse.Insfrastructure;

public static class ActivityExtensions
{
    public static Activity? StartSendActivity(IBasicProperties props)
    {
        var parentActivityContext = Activity.Current == null
            ? GetParentActivityContext(props)
            : default;

        var activity = Cached.Source.Value.CreateActivity("Send", ActivityKind.Producer, parentActivityContext);
        if (activity == null)
            return null;

        activity.Start();

        if (activity.Id != null)
        {
            props.Headers ??= new Dictionary<string, object?>();
            props.Headers["activity.id"] = activity.Id;
        }

        return activity;
    }

    public static Activity? StartReceiveActivity(IBasicProperties props)
    {
        var parentActivityContext = GetParentActivityContext(props, true);

        var activity = Cached.Source.Value.CreateActivity("receive", ActivityKind.Consumer, parentActivityContext);
        if (activity == null)
            return null;

        activity.Start();

        return activity;
    }

    private static ActivityContext GetParentActivityContext(IBasicProperties props, bool isRemote = false)
    {        
        if (props.Headers?.TryGetValue("activity.id", out var headerValue) == true)
        {
            if (headerValue is byte[] bytes)
            {
                var activityId = Encoding.UTF8.GetString(bytes);
                if (ActivityContext.TryParse(activityId, null, out var activityContext))
                {
                    if (isRemote && Activity.Current == null)
                        return new ActivityContext(activityContext.TraceId, activityContext.SpanId, activityContext.TraceFlags, activityContext.TraceState, isRemote);

                    return activityContext;
                }
            }
        }

        return default;
    }

    private static class Cached
    {
        internal static readonly Lazy<ActivitySource> Source = new Lazy<ActivitySource>(() =>
            new ActivitySource("Transport"));
    }
}
