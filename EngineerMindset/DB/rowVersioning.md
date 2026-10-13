# Row Versioning & Optimistic Concurrency in EF Core (SQL Server)
 
## 1. The problem: lost updates
 
Two users open the same record at the same time:
 
| Time | User A                         | User B                         |
|------|--------------------------------|--------------------------------|
| 1    | Reads Product (Price = 10)     | Reads Product (Price = 10)     |
| 2    | Changes Price to 12, saves     |                                |
| 3    |                                | Changes Name, saves            |
 
Without protection, User B's save silently overwrites User A's change. This is called a **lost update**.
 
## 2. What is row versioning?
 
Row versioning means each row carries a **version marker** that changes every time the row is modified. When you save, you say:
 
> "Update this row **only if** its version is still the one I originally read."
 
If someone else changed the row in between, the version no longer matches, the update affects **0 rows**, and you are told about the conflict instead of overwriting data.
 
This approach is called **optimistic concurrency**: you *optimistically* assume conflicts are rare, so you don't lock rows while the user is editing. You just detect conflicts at save time.
 
### SQL Server `rowversion`
 
- `rowversion` (formerly `timestamp`) is an 8-byte binary value.
- SQL Server **automatically changes it on every INSERT/UPDATE** of the row.
- It is unique per database and increases monotonically.
- It has nothing to do with date/time, despite the old name `timestamp`.
## 3. When to use it
 
Use row versioning when:
 
- Multiple users or services can edit the **same row** (web apps, APIs, background workers).
- Users read data, think/edit for a while, then save (long "think time"), so holding locks is not practical.
- Conflicts are **rare but would be harmful** (prices, stock quantity, balances, settings, documents).
- You want to avoid silent data loss without the cost of pessimistic locking.
You may **not** need it when:
 
- Only one writer ever touches the data.
- The data is append-only (e.g. logs, events).
- Conflicts are very frequent and you'd rather serialize access (consider pessimistic locking or a queue).
## 4. Simple example
 
### Entity
 
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
 
    [Timestamp] // marks this as a rowversion concurrency token
    public byte[] RowVersion { get; set; } = default!;
}
```
 
Fluent API alternative:
 
```csharp
modelBuilder.Entity<Product>()
    .Property(p => p.RowVersion)
    .IsRowVersion();
```
 
### What EF Core sends to SQL Server
 
```sql
UPDATE Products
SET Price = @p0
WHERE Id = @p1 AND RowVersion = @p2;   -- original version
```
 
- If the row version still matches: 1 row affected, save succeeds, SQL Server generates a new `RowVersion`.
- If someone else changed it: 0 rows affected, EF Core throws `DbUpdateConcurrencyException`.
### Reproducing a conflict
 
```csharp
// Two contexts simulate two users/requests
using var dbA = new AppDbContext();
using var dbB = new AppDbContext();
 
var productA = await dbA.Products.FirstAsync(p => p.Id == 1);
var productB = await dbB.Products.FirstAsync(p => p.Id == 1);
 
productA.Price = 12;
await dbA.SaveChangesAsync();          // OK, RowVersion changes in the DB
 
productB.Name = "New name";
await dbB.SaveChangesAsync();          // throws DbUpdateConcurrencyException
```
 
### Handling the exception
 
```csharp
try
{
    await db.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    var databaseValues = await entry.GetDatabaseValuesAsync();
 
    if (databaseValues is null)
    {
        // The row was deleted by someone else
        return NotFound();
    }
 
    // Common strategies:
 
    // 1) Database wins: discard my changes, reload current values
    await entry.ReloadAsync();
 
    // 2) Client wins: keep my values, overwrite the stored version and retry
    // entry.OriginalValues.SetValues(databaseValues);
    // await db.SaveChangesAsync();
 
    // 3) Merge manually: compare entry.CurrentValues vs databaseValues,
    //    or show both to the user and let them decide
}
```
 
## 5. Using it in a Web API (important detail)
 
In a web app the entity is read in one request and saved in another, so the original `RowVersion` must travel with the client:
 
```csharp
public record UpdateProductDto(int Id, string Name, decimal Price, byte[] RowVersion);
 
[HttpPut]
public async Task<IActionResult> Update(UpdateProductDto dto)
{
    var product = await db.Products.FindAsync(dto.Id);
    if (product is null) return NotFound();
 
    // Tell EF which version the client originally saw
    db.Entry(product).Property(p => p.RowVersion).OriginalValue = dto.RowVersion;
 
    product.Name = dto.Name;
    product.Price = dto.Price;
 
    try
    {
        await db.SaveChangesAsync();
        return NoContent();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Conflict("The record was modified by someone else. Reload and try again.");
    }
}
```
 
Without setting `OriginalValue`, EF would use the value it just loaded, and the conflict would go undetected.
 
## 6. Key points
 
- `rowversion` is generated and updated **by SQL Server**, never by your code.
- EF Core adds the version to the `WHERE` clause and checks the **affected row count**.
- 0 rows affected leads to `DbUpdateConcurrencyException`.
- EF Core **never merges automatically**; your code decides how to resolve conflicts.
- No locks are held between read and save, so it scales well.
- The version is per **row**, not per column, so two users editing different columns of the same row still conflict.
## 7. Alternatives
 
| Approach | Notes |
|----------|-------|
| `[ConcurrencyCheck]` on a normal property | Compares that property's original value in `WHERE`; works on any database but only protects that column |
| Manual version column (int/Guid) | You increment or regenerate it yourself; portable but easy to forget |
| Pessimistic locking (`UPDLOCK`, transactions) | Prevents conflicts rather than detecting them, but reduces concurrency |
| PostgreSQL `xmin` | Equivalent system column for PostgreSQL via Npgsql |
