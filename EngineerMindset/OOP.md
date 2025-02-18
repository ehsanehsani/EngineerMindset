# What is Object-Oriented Programming (OOP)?

OOP is a way of programming where we build programs using objects. Objects are created from classes, which act as blueprints. This makes our code easier to organize, reuse, and expand.

### There are four main principles of OOP, often remembered as P.I.E. + A:

1. **Polymorphism** – means different classes can be used (treated) in the same way, typically through method overriding or interfaces.
2. **Inheritance** – Creating new classes based on existing ones to reuse and extend behavior.
3. **Encapsulation** – Hiding the internal details of an object and exposing only what is necessary.
4. **Abstraction** – Hiding complex implementation details and exposing only essential features.

### Example in C#

#### Encapsulation (Hiding data using private and exposing with public methods)
```csharp
class Car
{
    private int speed; // Private field (hidden)

    public void SetSpeed(int newSpeed) // Public method to set speed
    {
        if (newSpeed > 0) speed = newSpeed;
    }

    public int GetSpeed() // Public method to get speed
    {
        return speed;
    }
}

```

#### Inheritance (Reusing code from a base class)
```csharp
class ElectricCar : Car // ElectricCar inherits from Car
{
    public int BatteryLevel { get; set; }

    public void ChargeBattery()
    {
        BatteryLevel = 100;
        Console.WriteLine("Battery fully charged!");
    }
}

```

#### Polymorphism (Overriding behavior in a derived class)
```csharp
class GasCar : Car
{
    public void Refuel()
    {
        Console.WriteLine("Car refueled!");
    }

    public override string ToString() // Overriding the default ToString() method
    {
        return "This is a gas-powered car.";
    }
}

```

#### Polymorphism via Interfaces
```csharp
// Define an interface
public interface IDriveable
{
    void Drive();
}

// Car class implementing IDriveable
public class Car : IDriveable
{
    public void Drive()
    {
        Console.WriteLine("Car is driving...");
    }
}

// Truck class implementing IDriveable
public class Truck : IDriveable
{
    public void Drive()
    {
        Console.WriteLine("Truck is driving...");
    }
}

// Main program
class Program
{
    static void Main()
    {
        IDriveable myCar = new Car();   // Treat Car as IDriveable
        IDriveable myTruck = new Truck(); // Treat Truck as IDriveable

        myCar.Drive();   // Calls Car's Drive()
        myTruck.Drive(); // Calls Truck's Drive()
    }
}

```


#### Abstraction (Hiding implementation details)
```csharp
abstract class Vehicle
{
    public abstract void Drive(); // Abstract method with no implementation
}

class Motorcycle : Vehicle
{
    public override void Drive() // Concrete implementation
    {
        Console.WriteLine("Motorcycle is driving...");
    }
}
```

Why? The Vehicle class defines the Drive() method but doesn't implement it—forcing subclasses like Motorcycle to provide their own implementation.
