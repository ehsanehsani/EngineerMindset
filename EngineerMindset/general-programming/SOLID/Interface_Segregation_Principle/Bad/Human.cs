namespace SOLID.ISP.Bad;

// ✅ Human can implement all methods naturally
public class Human : IWorker
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

