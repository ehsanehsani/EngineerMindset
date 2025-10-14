namespace SOLID.LSP.Good;

// ✅ GOOD: Square manages its own properties
// It doesn't inherit from Rectangle, avoiding the LSP violation
public class Square : IShape
{
    public int Side { get; set; }

    public Square(int side)
    {
        Side = side;
    }

    public int GetArea()
    {
        return Side * Side;
    }
}

// ✅ Now both shapes can be used interchangeably through IShape
// IShape shape1 = new Rectangle(5, 10);
// IShape shape2 = new Square(5);
// Both work as expected without breaking behavior!

