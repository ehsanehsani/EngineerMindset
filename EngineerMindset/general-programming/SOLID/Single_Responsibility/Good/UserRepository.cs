using SOLID.Single_Responsibility.Bad;

class UserRepository
{
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
}