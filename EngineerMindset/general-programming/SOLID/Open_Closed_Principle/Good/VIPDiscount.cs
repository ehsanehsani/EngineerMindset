namespace SOLID.Open_Closed_Principle.Good
{
    public class VIPDiscount : IDiscount
    {
        public double ApplyDiscount(double price) => price * 0.2;
    }
}
