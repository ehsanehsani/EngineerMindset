using SOLID.Single_Responsibility.Bad;

class EmailService
{
    public void SendEmail(User user, string message)
    {
        Console.WriteLine($"Sending email to {user.Email}: {message}");
    }

    public void SendWelcomeEmail(User user)
    {
        Console.WriteLine($"Welcome email sent to {user.Email}.");
    }

    public void SendPasswordResetEmail(User user)
    {
        Console.WriteLine($"Password reset email sent to {user.Email}.");
    }
}
