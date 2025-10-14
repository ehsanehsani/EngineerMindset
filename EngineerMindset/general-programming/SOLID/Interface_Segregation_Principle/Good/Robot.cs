namespace SOLID.ISP.Good;

// ✅ GOOD: Robot only implements what it needs
public class Robot : IWorkable
{
    public void Work()
    {
        Console.WriteLine("Robot is working");
    }

    // ✅ No need to implement Eat() or Sleep()
    // Robot only implements the interfaces it actually uses!
}

