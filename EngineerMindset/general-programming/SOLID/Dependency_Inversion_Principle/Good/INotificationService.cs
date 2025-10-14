namespace SOLID.DIP.Good;

// ✅ GOOD: Abstraction that both high-level and low-level modules depend on
public interface INotificationService
{
    void SendNotification(string message);
}

