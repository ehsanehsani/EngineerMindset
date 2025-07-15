namespace SOLID.Open_Closed_Principle.Bad
{
    public class Discount
    {
        public double CalculateDiscount(string customerType, double price)
        {
            if (customerType == "Regular") return price * 0.1;
            if (customerType == "VIP") return price * 0.2;
            return 0;
        }
    }
}
