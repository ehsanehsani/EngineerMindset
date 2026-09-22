# Differences between IEnumerable and IQueryable in C#

This document explains the differences between `IEnumerable<T>` and `IQueryable<T>` in C#, with examples and key concepts like deferred execution, expression trees, and a brief explanation of `IEnumerator`.

---

## Overview

### IEnumerable
- **Purpose:** Ideal for querying in-memory collections.
- **Execution:** Executes queries in memory. Even though the query can be deferred, further operations might be performed using LINQ-to-Objects (client-side).
- **Usage Scenario:** Suitable for small datasets or when data is already loaded into memory.

### IQueryable
- **Purpose:** Designed for querying remote data sources (e.g., databases).
- **Execution:** Builds an expression tree that is translated into a SQL query. Execution is deferred until the query is enumerated.
- **Usage Scenario:** Ideal for large datasets or when you need to build dynamic queries that execute on the server side.

---

## Code Examples

### 1. Using IQueryable

When querying a database using `IQueryable`, the query is built as an expression tree and is not executed until you iterate over it. For example:

```csharp
using (var context = new SchoolContext())
{
    // Build the query; this query remains as an expression tree (IQueryable)
    IQueryable<Student> query = context.Students.Where(s => s.Age > 18);

    // Execution happens when you iterate over the query (e.g., in a foreach loop)
    foreach (Student student in query)
    {
        Console.WriteLine(student.Name);
    }
}
```

### Explanation:

Deferred Execution:
The query is not executed until you start iterating over it.
Server-side Filtering:
The filtering condition (s.Age > 18) is translated into SQL, so only matching records are fetched from the database.

```csharp
IEnumerable<Employee> listE = dc.Employees.Where(p => p.Name.StartsWith("H"));
IQueryable<Employee> listQ = dc.Employees.Where(p => p.Name.StartsWith("H"));
```

### Both Queries:
The filtering condition (p.Name.StartsWith("H")) is applied on the database side when the query is executed (e.g., via enumeration or by calling .ToList()).

#### Further Filtering:

For IQueryable (listQ):
Adding additional filters, such as:

```csharp
listQ = listQ.Where(p => p.Salary > 50000);
```
appends the condition to the existing expression tree. When executed, the complete query (both filters) is translated into SQL and processed on the server, avoiding unnecessary data load.

For IEnumerable (listE):
Adding additional filters:

```csharp
listE = listE.Where(p => p.Salary > 50000);
```
applies the filter in memory using LINQ-to-Objects. This may result in loading more data into memory before filtering, reducing efficiency.

# What is an Expression tree? (`Expression<Func<T>>` vs `Func<T>`)

This is the usual “what is Expression?” question in a LINQ / EF round. It is **not** a new C# 12 feature (that one is **collection expressions**: `[1, 2, 3]`).

A lambda can be stored two ways:

```csharp
Func<Student, bool> fn = s => s.Age > 18;
Expression<Func<Student, bool>> expr = s => s.Age > 18;
```

They look the same. They are not.

- **`Func<...>`** — already compiled. `fn(student)` runs in your process (LINQ-to-Objects, in memory).
- **`Expression<Func<...>>`** — **code as data**. A tree of nodes: property `Age`, operator `>`, constant `18`. You cannot call it directly. EF / `IQueryable` **walks the tree** and generates SQL: `WHERE Age > 18`. If you really want to run it in memory, you `.Compile()` it into a `Func`.

That is why `IQueryable.Where` takes an expression and `IEnumerable.Where` takes a `Func`. Same lambda syntax; different parameter type. The compiler builds the tree only when the method expects `Expression<...>`.

---