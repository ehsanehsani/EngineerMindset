# Open/Closed Principle (OCP)

The **Open/Closed Principle** states that a class should be **open for extension but closed for modification**. This means you should be able to add new functionality without altering existing code. This is typically achieved through **abstraction, inheritance, or composition**.

### Example in C#
```csharp
// Base discount class
public abstract class Discount
{
    public abstract double ApplyDiscount(double price);
}

// Extended functionality without modifying existing code
public class SeasonalDiscount : Discount
{
    public override double ApplyDiscount(double price) => price * 0.9; // 10% off
}

public class BlackFridayDiscount : Discount
{
    public override double ApplyDiscount(double price) => price * 0.7; // 30% off
}

// Usage
Discount discount = new SeasonalDiscount();
double discountedPrice = discount.ApplyDiscount(100);
Console.WriteLine(discountedPrice);
```
By following OCP, we can introduce new discount types **without modifying** the existing `Discount` class, keeping our code **scalable and maintainable**.

