# What is Functional Programming?  
_A Practical Introduction for C# Developers_

Functional programming (FP) is a programming paradigm—a way to think about writing code—centered around _functions_ and _immutable data_. If you’re a mid-range C# programmer and have mostly worked with object-oriented or procedural code, FP might seem unfamiliar. This article will introduce the core concepts, compare FP to traditional approaches, show why it matters, and provide C# examples you can understand and use.

---

## 1. What is Functional Programming?

Functional programming treats _computation as the evaluation of mathematical functions_ and _avoids changing state or mutable data_. In FP, you build programs by composing pure functions.

**Key Principles:**
- **Pure Functions:** Functions always produce the same output for the same input and have no side effects.
- **Immutability:** Data does not change once created.
- **First-Class Functions:** Functions can be assigned to variables, passed as arguments, or returned from other functions.
- **Declarative Style:** Focus on _what_ to do, not _how_ to do it.

---

## 2. Why Use Functional Programming?

Functional programming can make code:
- **Easier to test and debug** (no hidden state or side effects)
- **Safer for concurrency and parallelism** (immutable data)
- **More predictable** (pure functions)
- **Expressive and concise** (function composition)

---

## 3. Functional vs Non-Functional (Imperative) Programming

Let’s compare a simple task:  
**Given a list of integers, return a list of their squares.**

### Imperative (Non-Functional) C#

```csharp
List<int> SquareNumbersImperative(List<int> numbers)
{
    List<int> result = new List<int>();
    foreach (int n in numbers)
    {
        result.Add(n * n);
    }
    return result;
}
```
- **How it works:**  
  - Create a new list.
  - Loop over each number, compute the square, and add to the list.

_This approach mutates variables and uses explicit loops._

### Functional C# (using LINQ and lambda expressions)

```csharp
List<int> SquareNumbersFunctional(List<int> numbers)
{
    return numbers.Select(n => n * n).ToList();
}
```
- **How it works:**  
  - `Select` applies a function (`n => n * n`) to each item.
  - No explicit loop, no mutation, more concise.

_This approach focuses on what you want (“map each number to its square”) instead of how._

---

## 4. Core Concepts Explained

### A. **Pure Functions vs Side Effects**

Let’s compare two functions:

```csharp
int Add(int a, int b) => a + b; // Pure

int AddAndLog(int a, int b)
{
    Console.WriteLine(a + b); // Has side effect
    return a + b;
}
```

#### 1. The Pure Function

`Add(int a, int b)` is a **pure function** because:
- It always produces the same output for the same input.
- It does not modify anything outside of itself.
- It simply computes and returns a value.

**Example:**  
Calling `Add(2, 3)` always returns `5`, and nothing else happens.

#### 2. The Function with Side Effect

`AddAndLog(int a, int b)` is **not pure** because:
- In addition to returning a value, it prints to the console (`Console.WriteLine`).
- Printing to the console changes the state of the outside world (the screen), which is a _side effect_.

**Example:**  
Calling `AddAndLog(2, 3)` returns `5`, and also prints `5` to the console.

#### What is a "Side Effect"?

A **side effect** is anything a function does besides returning a value, such as:
- Modifying a variable outside itself
- Printing to the console
- Writing to a file
- Sending data over a network

Pure functions have **no side effects**; they just compute and return a result.

#### Why Does This Matter?

- **Pure functions** are predictable and easy to test.
- Functions with **side effects** can lead to unexpected behaviors and are harder to test and debug.

> **Summary:**  
> `Add` is pure—just returns a value.  
> `AddAndLog` is not pure—it returns a value and does something else (prints).

---

### B. **Immutability**
Immutable objects can't be changed after they're created.

**Example (using records in C#):**
```csharp
public record Person(string Name, int Age);

var p1 = new Person("Alice", 30);
var p2 = p1 with { Age = 31 }; // Creates a new person, original unchanged
```

### C. **Higher-Order Functions**
Functions that take other functions as arguments or return functions.

**Example:**
```csharp
Func<int, int> square = n => n * n;
List<int> squares = numbers.Select(square).ToList();
```

---

## 5. Functional Programming in C#

While C# is primarily object-oriented, it supports many FP concepts:

- **Lambda expressions**: `x => x * 2`
- **LINQ**: `Where`, `Select`, `Aggregate`, etc.
- **Immutable types**: `record` types, `ImmutableList<T>`
- **Delegates**: `Func<T>`, `Action<T>`

**Example Task: Filter, Map, and Reduce**

Suppose you want the sum of squares of all even numbers in a list:

### Imperative:

```csharp
int SumOfSquaresImperative(List<int> numbers)
{
    int sum = 0;
    foreach (int n in numbers)
    {
        if (n % 2 == 0)
        {
            sum += n * n;
        }
    }
    return sum;
}
```

### Functional:

```csharp
int SumOfSquaresFunctional(List<int> numbers)
{
    return numbers.Where(n => n % 2 == 0)
                  .Select(n => n * n)
                  .Sum();
}
```

---

## 6. Common Functional Programming Languages

Some languages designed for FP:
- **Haskell**
- **Erlang**
- **F#** (Microsoft’s functional language, works with .NET!)
- **Scala**
- **Clojure**
- **Elixir**

But most modern languages (C#, JavaScript, Python) now include FP features.

---

## 7. Real-World Usage

Functional programming shines in:
- **Data transformations** (e.g., filtering, mapping, reducing collections)
- **Concurrency** (immutable data prevents race conditions)
- **Complex business logic** (expressed as composable functions)
- **Reactive programming** (event streams, observables)

**Examples in practice:**
- Building APIs with predictable outputs
- Data pipelines in analytics
- Parallel processing
- UI programming (React uses FP concepts)

---

## 8. Summary

Functional programming is about writing predictable, composable code by focusing on pure functions and immutability. You don’t have to abandon everything you know from C#—instead, you can blend FP ideas with your current skills to write better, cleaner code.

> **Start small:** Try using LINQ and lambda expressions. Refactor for immutability. Write pure functions.  
> **Next steps:** Explore F#, learn more about FP patterns, and practice!
