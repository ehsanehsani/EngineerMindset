# The difference between PATCH and PUT in HTTP:

. **PUT** updates the entire resource. If a field is missing, it may be removed or reset.

. **PATCH** updates only the specified fields without affecting others.

### Scenario: Updating a User Profile
```json
{
  "name": "John Doe",
  "email": "john@example.com",
  "age": 30
}
```

#### PUT Request: Replaces the whole profile.
```http
PUT /users/1  
Content-Type: application/json  

{
  "name": "John Smith",
  "email": "johnsmith@example.com",
  "age": 31
}
```

**Effect**: Updates all fields. If "age" is missing, it might be removed or reset.

#### PATCH Request: Updates only specific fields.
```http
PATCH /users/1  
Content-Type: application/json  

{
  "name": "John Smith"
}
```
**Effect**: Only changes "name" to "John Smith"; "email" and "age" stay the same.


> With PATCH, you send only the changed fields, so other fields remain unchanged.

> With PUT, you must send all fields, even the ones that haven’t changed.

## **PUT** is idempotent
Because sending the same full update multiple times won’t change the result after the first time.

## PATCH is not always idempotent.
Because it depends on how it's implemented.
. If it just updates the email → It might be idempotent.
. But if it does something like “add 1 to user score” → Each request changes the result → Not idempotent.
