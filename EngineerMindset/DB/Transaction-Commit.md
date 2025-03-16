# SQL Transaction
### Commit and rollback:

If you have to update a table by query in SQL and if it was the only option, it's important to ensure that changes are safe, especially when you need to validate the number of affected rows. Using a transaction with TRY...CATCH allows you to rollback changes if the update doesn’t match the expected row count. Here's a complete template you can use to check the affected rows and handle commits or rollbacks:

```sql
DECLARE @ExpectedRows INT;  
SET @ExpectedRows = 1;  -- Set the number of rows you expect to be updated  

BEGIN TRY  
    BEGIN TRANSACTION  

    -- Your Update Query  
    UPDATE YourTable  
    SET ColumnName = 'NewValue'  
    WHERE SomeCondition;  

    -- Store the affected rows count in a variable before the COMMIT/ROLLBACK
    DECLARE @AffectedRows INT = @@ROWCOUNT;

    -- Check if the number of rows affected is correct
    IF @AffectedRows = @ExpectedRows  
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'Transaction committed successfully.';
    END
    ELSE  
    BEGIN
        ROLLBACK TRANSACTION;
		PRINT 'Transaction rolled back. Expected row count: ' + CONVERT(VARCHAR, @ExpectedRows) + 
			  ', but actual row count was: ' + CONVERT(VARCHAR, @AffectedRows) + '.';
    END
END TRY  
BEGIN CATCH  
    ROLLBACK TRANSACTION;  
    PRINT 'Transaction failed and rolled back.';  
END CATCH

```