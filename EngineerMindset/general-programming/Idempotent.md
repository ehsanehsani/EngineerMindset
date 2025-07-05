# Understanding Idempotent Operations

## 🔍 What Does "Idempotent" Mean in General?

**Idempotent** is a concept from mathematics and computing.  
An operation is called **idempotent** if doing it once has the same effect as doing it multiple times.

> 📌 In simple words:  
> **"Doing it once or doing it many times — the result stays the same."**

### 🧽 Real-World Example:
Imagine a **light switch** that turns the light **off**.

- If the light is ON and you press the OFF button → the light turns OFF ✅
- If the light is already OFF and you press the OFF button again → nothing changes ✅
- Pressing "OFF" many times still results in the light being OFF ✅

➡️ This **OFF operation is idempotent** — no matter how many times you do it, the result (light OFF) stays the same.

---

# Idempotent in HTTP Methods

In HTTP, some request types (like PUT or GET) are idempotent — calling them multiple times won’t change the final result.

---

## ✅ Example of Idempotent Method: PUT

### Scenario:
You want to update a user profile.

#### Request:

```http

PUT /users/1
Content-Type: application/json

{
  "name": "Ali",
  "age": 30
}
```

First call → updates the user to name: Ali, age: 30.

Second call → same data, nothing changes.

Third, fourth... still the same.

✅ Final Result:
The user’s profile always ends up with name: "Ali" and age: 30.

✅ PUT is idempotent.
