namespace SOLID.DIP.Good;

// ✅ GOOD: Another implementation of the abstraction
public class SmsNotification : INotificationService
{
    public void SendNotification(string message)
    {
        Console.WriteLine($"SMS sent: {message}");
    }
}

