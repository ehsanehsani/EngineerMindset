namespace SOLID.Single_Responsibility.Bad
{
    public class User
    {
        public string Name { get; set; }
        public string Email { get; set; }

        public void SaveToDatabase(User user)
        {
            Console.WriteLine("User saved to database.");
        }

        public void UpdateUser(User user)
        {
            Console.WriteLine($"User {user.Name} updated in the database.");
        }

        public void DeleteUser(User user)
        {
            Console.WriteLine($"User {user.Name} deleted from database.");
        }

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
}
// ❌ BAD DESIGN - Violates the Single Responsibility Principle (SRP)
//
// This User class has **two responsibilities**:
// 1. Managing user data (Name, Email)
// 2. Handling database storage (SaveToDatabase)
// 3. Handling email sending (SendEmail)
//
// 🚨 PROBLEMS with this approach:
// - **Multiple reasons to change**: If we change how users are stored OR how emails are sent, we have to modify this class.
// - **Harder to maintain**: Over time, adding more logic (e.g., logging emails, switching database types) will make this class huge and complex.
// - **Difficult for multiple developers**: If one developer updates database logic and another modifies email functionality, they both need to change the same file, leading to merge conflicts.
// - **Risk of breaking unrelated functionality**: A bug in email sending might accidentally affect database logic.
//
// ✅ SOLUTION: Separate responsibilities into different classes
// - `User` → Stores user data only
// - `UserRepository` → Handles saving users to the database
// - `EmailService` → Handles sending emails
//
// This makes the code more modular, testable, and maintainable.