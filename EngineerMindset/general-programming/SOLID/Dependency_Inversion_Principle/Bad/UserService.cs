namespace SOLID.DIP.Bad;

// ❌ BAD: UserService depends directly on a concrete class
public class UserService
{
    private EmailNotification _emailNotification;

    public UserService()
    {
        // ❌ Tight coupling: directly creating the dependency
        _emailNotification = new EmailNotification();
    }

    public void RegisterUser(string username)
    {
        Console.WriteLine($"User {username} registered");
        
        // ❌ Can only send emails, can't switch to SMS or push notifications
        _emailNotification.SendEmail($"Welcome {username}!");
    }
}

// ❌ Problems:
// 1. Hard to test (can't mock EmailNotification)
// 2. Can't switch to SMS or other notification methods without changing UserService
// 3. UserService is tightly coupled to EmailNotification
// 4. Violates Dependency Inversion: high-level module depends on low-level module

