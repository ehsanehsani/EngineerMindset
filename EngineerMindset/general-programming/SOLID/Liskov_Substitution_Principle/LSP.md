# 📌 Liskov Substitution Principle (LSP)

## ❓ What is LSP?
The **Liskov Substitution Principle (LSP)** states that **objects of a derived class should be able to replace objects of the base class without breaking the program**. In simpler terms, if you have a parent class and a child class, you should be able to use the child class anywhere you use the parent class, and everything should still work correctly.

---

## 🚀 Real-World Analogy
Imagine you have a **remote control** that works with **any TV**. If someone gives you a "Smart TV," the remote should still work the same way. If the Smart TV requires completely different buttons or behaves unexpectedly when you press the volume button, it **violates LSP** because it can't properly substitute a regular TV.

---

## ⚠️ The Classic Rectangle-Square Problem

### 🔴 Bad Example (Violates LSP)
Mathematically, a square **is a** rectangle (a rectangle with equal sides). So, it might seem logical to make `Square` inherit from `Rectangle`. However, this creates a problem:

```csharp
public class Rectangle
{
    public virtual int Width { get; set; }
    public virtual int Height { get; set; }

    public int GetArea() => Width * Height;
}

public class Square : Rectangle
{
    public override int Width
    {
        set { base.Width = value; base.Height = value; } // Changes both!
    }

    public override int Height
    {
        set { base.Width = value; base.Height = value; } // Changes both!
    }
}
```

**Why is this bad?**
```csharp
Rectangle rect = new Square();
rect.Width = 5;
rect.Height = 10;
Console.WriteLine(rect.GetArea()); // Expected: 50, Actual: 100 ❌
```

When we use `Square` as a `Rectangle`, it **doesn't behave like a rectangle**. Setting `Width` and `Height` independently should work, but with `Square`, they affect each other. This **breaks the expected behavior** of `Rectangle`, violating LSP.

---

## ✅ Solution: Use Abstraction

Instead of forcing inheritance where it doesn't fit, use a common interface:

```csharp
public interface IShape
{
    int GetArea();
}

public class Rectangle : IShape
{
    public int Width { get; set; }
    public int Height { get; set; }

    public Rectangle(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int GetArea() => Width * Height;
}

public class Square : IShape
{
    public int Side { get; set; }

    public Square(int side)
    {
        Side = side;
    }

    public int GetArea() => Side * Side;
}
```

**Why is this better?**
- Both `Rectangle` and `Square` implement `IShape` independently.
- Each class maintains its own properties and behavior.
- You can use either shape through the `IShape` interface without unexpected behavior.
- No inheritance relationship that forces unnatural constraints.

```csharp
IShape shape1 = new Rectangle(5, 10);
IShape shape2 = new Square(5);

Console.WriteLine(shape1.GetArea()); // 50 ✅
Console.WriteLine(shape2.GetArea()); // 25 ✅
```

---

## 🎤 How to Explain LSP
💡 *"If you replace a parent class with a child class, the program should still work correctly without any surprises. The child class shouldn't change the expected behavior of the parent class. If substituting a child class breaks things or causes unexpected behavior, it violates LSP."*

---

## 🎯 Key Takeaways
- **LSP ensures derived classes can substitute their base classes without breaking functionality.**
- **Just because something "is a" relationship exists in real life doesn't mean inheritance is the right choice in code.**
- **Use interfaces or abstract classes when objects share behavior but not implementation details.**
- **Violating LSP leads to unexpected bugs when using polymorphism.**

---

🔥 Following LSP makes your code more reliable and prevents subtle bugs when working with inheritance! 🚀

