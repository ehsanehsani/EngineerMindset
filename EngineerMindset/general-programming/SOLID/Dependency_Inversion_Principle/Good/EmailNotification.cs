namespace SOLID.DIP.Good;

// ✅ GOOD: Concrete implementation of the abstraction
public class EmailNotification : INotificationService
{
    public void SendNotification(string message)
    {
        Console.WriteLine($"Email sent: {message}");
    }
}

