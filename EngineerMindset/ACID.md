# ACID in Database Transactions

ACID stands for **Atomicity**, **Consistency**, **Isolation**, and **Durability**. These are the four properties that ensure reliable processing of database transactions. Each property plays a crucial role in maintaining data integrity and ensuring that transactions are processed correctly, even in the face of errors, power outages, or other issues.

---

## 1. **Atomicity**: All or Nothing

**Atomicity** means that a transaction is treated as a single unit, which either completes in its entirety or doesn’t execute at all. If any part of the transaction fails, the entire transaction is rolled back to the state it was in before the transaction started.

### Example:
Imagine you’re transferring money between two bank accounts:
- Withdraw €100 from **Account A**.
- Deposit €100 into **Account B**.

If the system crashes after the withdrawal but before the deposit, **Atomicity** ensures that the entire transaction is **rolled back**. Neither account will be affected, and you won’t lose money.

---

## 2. **Consistency**: Valid Data Before and After

**Consistency** ensures that a transaction takes the database from one valid state to another. If a transaction violates any integrity constraints (e.g., negative stock in an inventory system), the database will reject the transaction and prevent any changes.

### Example: Stock Management
You have a product with **10** items in stock. A customer attempts to buy **11** items, but your system should not allow this because the stock will go below zero.

- **Before Transaction**: Stock = 10
- **Transaction**: Customer buys 11 items
- **After Transaction**: Stock should be 9 (if valid), but the system must reject the transaction because Stock cannot be negative.

This ensures that the database never ends up with **invalid data**, and the transaction does not proceed if it violates any rules or constraints.

### How the Database Enforces Consistency:
The database can use **constraints** like:
- `CHECK(Stock >= 0)` to prevent negative stock
- **Foreign keys** to ensure data integrity across tables

---

## 3. **Isolation**: Transactions Don’t Interfere

**Isolation** ensures that transactions are processed independently of each other. Even if multiple transactions are happening at the same time, each transaction should behave as if it’s the only one running.

### Example: Two Customers Buying the Last Product

Imagine two customers trying to buy the last product in stock at the same time:
- **Customer A** buys the last product (Stock = 1).
- **Customer B** also tries to buy it, but the system must prevent them from doing so until Customer A’s transaction is completed.

With **Isolation**, the system ensures that **Customer B** doesn’t get access to the same product until Customer A’s transaction is committed or rolled back. This prevents **race conditions** where two transactions could conflict with each other.

---

## 4. **Durability**: Permanent Changes After Commit

Once a transaction is successfully committed, the changes made to the database are permanent, even in the event of a system crash. **Durability** guarantees that committed data will persist, and no information will be lost after the transaction is finished.

### Example: Hotel Reservation

A customer successfully books a hotel room. If the system crashes right after the booking is confirmed:
- The **booking should not disappear** after the system is restored.
- The database should retain the customer’s reservation, and it should still be visible when the system recovers.

**Durability** ensures that the data is saved to a stable storage medium (like disk or SSD) and is never lost once committed.

---

## Conclusion

ACID is crucial for maintaining data integrity and ensuring the reliability of transactions in database systems. It guarantees that:
- **Atomicity** ensures all-or-nothing transactions.
- **Consistency** maintains valid data before and after a transaction.
- **Isolation** ensures transactions don’t interfere with each other.
- **Durability** ensures data is permanent after a commit.

By adhering to ACID principles, databases can safely handle multiple transactions and maintain data integrity even in the face of errors or system failures.

---
