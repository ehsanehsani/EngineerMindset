namespace SOLID.DIP.Good;

// ✅ GOOD: UserService depends on abstraction, not concrete implementation
public class UserService
{
    private readonly INotificationService _notificationService;

    // ✅ Dependency is injected through constructor
    public UserService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public void RegisterUser(string username)
    {
        Console.WriteLine($"User {username} registered");
        
        // ✅ Works with any INotificationService implementation
        _notificationService.SendNotification($"Welcome {username}!");
    }
}

// ✅ Benefits:
// 1. Easy to test (can inject mock INotificationService)
// 2. Can switch implementations without changing UserService:
//    - var service = new UserService(new EmailNotification());
//    - var service = new UserService(new SmsNotification());
// 3. Loose coupling: UserService doesn't know about concrete implementations
// 4. Follows Dependency Inversion: both depend on abstraction (INotificationService)

