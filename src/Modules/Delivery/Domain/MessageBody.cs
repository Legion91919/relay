using Relay.Delivery.Contracts;

namespace Relay.Delivery.Domain;

internal static class MessageBody
{
    public static BodyProblem Validate(string? body, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return BodyProblem.Required;
        }

        return body.Length > maxLength ? BodyProblem.TooLong : BodyProblem.None;
    }
}
