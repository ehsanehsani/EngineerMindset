namespace SOLID.ISP.Bad;

// ❌ BAD: Fat interface with methods not all implementers need
public interface IWorker
{
    void Work();
    void Eat();
    void Sleep();
}

