# SOLID Principles in C#

The **SOLID** principles are five fundamental guidelines for object-oriented programming that help improve code maintainability, readability, and scalability. They promote a clean and flexible design that is easy to extend and refactor.

## 1. **Single Responsibility Principle (SRP)**
A class should have **one** reason to change. This means that a class should only be responsible for one functionality. If a class has more than one responsibility, it will be harder to change and maintain. By keeping a class focused on one task, you can improve code readability and reduce the risk of bugs when making changes.

### Key Idea:
- **One responsibility** → **One reason to change**

## 2. **Open/Closed Principle (OCP)**
A class should be **open for extension** but **closed for modification**. This means you should be able to add new functionality to a class without altering its existing code. This can be achieved by using abstraction, inheritance, or interfaces to extend behavior, thus preserving the integrity of the existing class.

### Key Idea:
- **Extend** functionality without modifying the existing code.

## 3. **Liskov Substitution Principle (LSP)**
Objects of a derived class should be able to **substitute** objects of the base class without affecting the correctness of the program. In other words, subclasses should be interchangeable with their parent class and should honor the contract set by the base class. Violating LSP can lead to unexpected behavior when subclasses are used in place of base class instances.

### Key Idea:
- **Substitute** derived classes for base class instances.

## 4. **Interface Segregation Principle (ISP)**
Clients should not be forced to depend on interfaces they do not use. Instead of having one large interface with many methods, it's better to have several smaller, more specific interfaces. This ensures that classes only implement the methods they actually need, leading to a more cohesive design.

### Key Idea:
- **Smaller, more focused interfaces** instead of large ones.

## 5. **Dependency Inversion Principle (DIP)**
High-level modules should not depend on low-level modules. Both should depend on **abstractions**. Additionally, **abstractions should not depend on details**, but details should depend on abstractions. This principle encourages decoupling classes, making code more flexible and easier to test by relying on abstractions like interfaces or abstract classes instead of concrete implementations.

### Key Idea:
- **Depend on abstractions**, not on concrete classes.
