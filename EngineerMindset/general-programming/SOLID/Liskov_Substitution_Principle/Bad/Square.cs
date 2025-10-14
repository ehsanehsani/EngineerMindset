namespace SOLID.LSP.Bad;

// ❌ BAD: Square violates LSP
// When you set Width, it also changes Height (and vice versa)
// This breaks the expectations of Rectangle's behavior
public class Square : Rectangle
{
    public override int Width
    {
        get => base.Width;
        set
        {
            base.Width = value;
            base.Height = value; // ❌ Changing width also changes height!
        }
    }

    public override int Height
    {
        get => base.Height;
        set
        {
            base.Width = value; // ❌ Changing height also changes width!
            base.Height = value;
        }
    }
}

// ❌ Problem demonstration:
// Rectangle rect = new Square();
// rect.Width = 5;
// rect.Height = 10;
// int area = rect.GetArea(); // Expected: 50, Actual: 100
// This breaks the substitution principle!

