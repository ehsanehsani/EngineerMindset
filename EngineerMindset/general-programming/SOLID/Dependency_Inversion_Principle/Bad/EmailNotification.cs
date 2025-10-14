namespace SOLID.DIP.Bad;

// ❌ Concrete implementation
public class EmailNotification
{
    public void SendEmail(string message)
    {
        Console.WriteLine($"Email sent: {message}");
    }
}

