# SQL Transaction
### Commit and rollback:

```sql

  -- First Select two first lines and run the query:

  BEGIN TRANSACTION
  Update Bids set Amount = 333 where Id = 786

  -- Then if the affected rows are correct: uncomment and Select "COMMIT TRANSACTION" and Run it:
  -- COMMIT TRANSACTION

  -- IF Not Correct: 
  -- ROLLBACK TRANSACTION

```