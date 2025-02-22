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

# Deferred Execution & Expression Trees
### Deferred Execution:
Both interfaces support deferred execution, meaning the query is not run until the data is actually needed (e.g., during enumeration). With IQueryable, the entire query remains part of the expression tree until execution, ensuring that all filtering happens on the database server.

### Expression Trees:
An expression tree is a data structure that represents code (such as lambda expressions) as a tree of expressions. In IQueryable, the expression tree is used to translate the LINQ query into an optimized SQL query that runs on the server.