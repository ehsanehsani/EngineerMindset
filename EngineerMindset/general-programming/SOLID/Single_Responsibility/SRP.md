# 📌 Single Responsibility Principle (SRP)

## ❓ What is SRP?
The **Single Responsibility Principle (SRP)** states that **a class should have only one reason to change**. This means that a class should only have one responsibility and should not mix concerns from different domains.

---

## 🚀 Misconception: "SRP means one method per class"
❌ **Wrong!** SRP does **not** mean you can only have one method per class.

✅ **Correct:** A class can have multiple methods, but they should all be related to a **single responsibility**.

### 🔹 Clarification
SRP does **NOT** mean "only one method per class." It means "one reason to change." 

For example, `UserRepository` has multiple methods (Save, Update, Delete), but they all belong to the responsibility of **managing user data**. Similarly, `EmailService` has multiple methods, but they all belong to **email functionality**. By keeping related methods together and separating concerns, we make the code **easier to maintain and extend**.

For example:
```csharp
class UserRepository
{
    public void SaveToDatabase(User user) { /* Save user */ }
    public void UpdateUser(User user) { /* Update user */ }
    public void DeleteUser(User user) { /* Delete user */ }
}
```
This class has multiple methods, but they all belong to the responsibility of **managing user data** (storage-related tasks). This follows SRP.

Similarly:
```csharp
class EmailService
{
    public void SendEmail(User user, string message) { /* Send email */ }
    public void SendWelcomeEmail(User user) { /* Send welcome email */ }
    public void SendPasswordResetEmail(User user) { /* Send password reset email */ }
}
```
All these methods are related to **email functionality**, so they belong together. This is also SRP-compliant.

---

## ⚠️ Why is SRP Important?
### 🔴 Problem with violating SRP:
If a class has multiple unrelated responsibilities, it can cause:
- **Merge conflicts**: Multiple developers working on different concerns (e.g., database + email) might have to modify the same class.
- **Harder maintenance**: If you modify one part (e.g., changing how users are stored), you might unintentionally break another part (e.g., email sending).
- **Difficult testing**: If a class mixes concerns, it becomes harder to write unit tests for just one part.

### ✅ Solution: Decoupling responsibilities
A better approach is to separate concerns into different classes:
```csharp
class UserService
{
    private EmailService _emailService;
    
    public UserService(EmailService emailService)
    {
        _emailService = emailService;
    }

    public void ChangeUserPassword(User user, string newPassword)
    {
        user.Password = newPassword;
        _emailService.SendEmail(user, "Your password has been changed.");
    }
}
```
**Why is this better?**
- `UserService` now handles business logic related to users.
- `EmailService` is still responsible **only** for sending emails.
- If we need to modify how passwords are changed, we update `UserService` **without affecting email logic**.

---

## 🎤 How to Explain SRP
💡 *"The problem with a class having multiple responsibilities is that unrelated changes affect each other. If one part of the logic changes, we might accidentally break another. By separating concerns, we make the code more maintainable, testable, and easier to extend without risk."*

---

## 🎯 Key Takeaways
- **SRP means a class should have only one reason to change, NOT just one method per class.**
- **Separate unrelated responsibilities into different classes** to avoid conflicts, make maintenance easier, and reduce unintended bugs.
- **Keep related methods together** within the same responsibility.

---

🔥 Following SRP makes your code cleaner, more modular, and easier to maintain! 🚀