CREATE FUNCTION [dbo].[MaxBigInt]
(
	@value1		BIGINT,
	@value2		BIGINT
)
RETURNS BIGINT
AS
BEGIN
	RETURN 
	CASE 
		WHEN @value1 is null THEN @value2
		WHEN @value2 is null THEN @value1
		WHEN @value1 < @value2 THEN @value2 
		ELSE @value1 
	END;
END;