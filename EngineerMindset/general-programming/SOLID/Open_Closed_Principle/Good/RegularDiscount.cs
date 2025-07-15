namespace SOLID.Open_Closed_Principle.Good
{
    public class RegularDiscount : IDiscount
    {
        public double ApplyDiscount(double price) => price * 0.1;
    }
}
