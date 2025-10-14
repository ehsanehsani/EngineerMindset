namespace SOLID.ISP.Bad;

// ❌ BAD: Robot is forced to implement methods it doesn't need
public class Robot : IWorker
{
    public void Work()
    {
        Console.WriteLine("Robot is working");
    }

    // ❌ Robots don't eat, but we're forced to implement this
    public void Eat()
    {
        throw new NotImplementedException("Robots don't eat!");
    }

    // ❌ Robots don't sleep, but we're forced to implement this
    public void Sleep()
    {
        throw new NotImplementedException("Robots don't sleep!");
    }
}

// ❌ Problem: Robot is forced to implement methods it doesn't use
// This violates ISP because clients depend on methods they don't need

