namespace SOLID.ISP.Good;

// ✅ GOOD: Human implements all interfaces it needs
public class Human : IWorkable, IFeedable, ISleepable
{
    public void Work()
    {
        Console.WriteLine("Human is working");
    }

    public void Eat()
    {
        Console.WriteLine("Human is eating");
    }

    public void Sleep()
    {
        Console.WriteLine("Human is sleeping");
    }
}

