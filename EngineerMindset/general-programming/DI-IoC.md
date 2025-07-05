# What do you know about Dependency Injection?
Dependency Injection is a design pattern used in software development to make our code more flexible, testable, and maintainable. Instead of a class creating its own dependencies, they are provided (or "injected") from the outside. This helps us follow the principles of loose coupling and separation of concerns.

In C#, we often use DI with interfaces and constructor injection. Here's a simple example:

#### Without Dependency Injection (Tightly Coupled):
```csharp
public class EmailService
{
    public void SendEmail(string message)
    {
        Console.WriteLine($"Sending Email: {message}");
    }
}

public class Notification
{
    private EmailService _emailService = new EmailService(); // tightly coupled

    public void Send(string message)
    {
        _emailService.SendEmail(message);
    }
}
```

Here, Notification is tightly coupled to EmailService. We can't easily replace EmailService (e.g. for testing or switching to SMS).

#### With Dependency Injection (Loosely Coupled):
```csharp
public interface IMessageService
{
    void Send(string message);
}

public class EmailService : IMessageService
{
    public void Send(string message)
    {
        Console.WriteLine($"Sending Email: {message}");
    }
}

public class Notification
{
    private readonly IMessageService _messageService;

    public Notification(IMessageService messageService) // Constructor Injection
    {
        _messageService = messageService;
    }

    public void Notify(string message)
    {
        _messageService.Send(message);
    }
}

```
Now Notification depends on an abstraction (IMessageService), not a concrete class. This makes testing and swapping implementations very easy.

### Using Built-in Dependency Injection in ASP.NET Core:
In real-world applications like ASP.NET Core, we register services like this in Startup.cs or Program.cs:

```csharp
services.AddTransient<IMessageService, EmailService>();
```

### Summary:

Dependency Injection improves code flexibility and testability.

It allows us to swap implementations without changing the consumer class.

In C#, we typically use constructor injection with interfaces.

DI is widely supported in ASP.NET Core via the built-in IoC container.

# What do you know about Inversion of Control (IoC)? How IoC and Dependency Injection (DI) Are Related
**Inversion of Control (IoC)** is a **software design principle** where the control of object creation and dependency management is handed over to an external component or framework, rather than being managed inside the class itself.

**Dependency Injection (DI)** is the most common way to implement IoC. It means providing a class’s dependencies from the outside instead of having the class create them itself. This makes your code more **flexible, loosely coupled, and easier to test.**

- Inversion of Control (IoC) is a principle where control of object creation is transferred to a container or framework.

- It leads to loose coupling and better testability.

- Dependency Injection is a specific implementation of IoC — probably the most common one today.
