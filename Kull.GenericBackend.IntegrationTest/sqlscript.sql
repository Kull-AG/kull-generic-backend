CREATE TABLE dbo.Pets (PetId int PRIMARY KEY , PetName varchar(100), IsNice bit, ts timestamp)
GO
CREATE TABLE dbo.TestDbVersion(VersionNr int)
GO
INSERT INTO dbo.TestDbVersion(VersionNr) VALUES(6) -- sync this value with the TestWebApplicationFactory.expectedVersion
GO
INSERT INTO dbo.Pets(PetId, PetName, IsNice)
SELECT 1, 'Dog', 0
UNION ALL 
SELECT 2, 'Dog 2', 1
GO
CREATE PROCEDURE spGetPets
	@OnlyNice bit=0,
	@SearchString varchar(100),
	@IpAddress varchar(100)
AS
BEGIN
	SELECT * FROM dbo.Pets
		WHERE IsNice=1 OR @OnlyNice=0
END
GO
CREATE PROCEDURE spGetPet
	@Petid int
AS
BEGIN
	SELECT * FROM dbo.Pets WHERE PetId=@PetId
END
GO
CREATE PROCEDURE spAddPet
	@PetName nvarchar(1000),
	@IsNice bit
AS
BEGIN
	-- Just pretending
	SELECT CONVERT(BIT,1) AS Success, 3 AS NewPetId
END
GO
CREATE PROCEDURE spDeletePet
	@Petid int
AS
BEGIN
	-- Just pretending
	SELECT CONVERT(BIT,1) AS Success
END
GO
CREATE PROCEDURE spUpdatePet
	@Petid int,
	@Ts timestamp
AS
BEGIN
	-- Just pretending
	SELECT CONVERT(BIT,1) AS Success
		FROM dbo.Pets WHERE PetId=@PetId AND ts=@Ts
END
GO
CREATE PROCEDURE spSearchPets
	@SearchString nvarchar(MAX)
AS
BEGIN 	
	SELECT* FROM Pets WHERE PetName LIKE '%' + @SearchString + '%'
END
GO
CREATE TYPE dbo.IdNameType AS TABLE 
(
	Id bigint, 
	Name nvarchar(1000), 
    PRIMARY KEY (Id)
)
GO
CREATE PROCEDURE dbo.spTestBackend
	@SomeId int,
	@Ids dbo.IdNameType readonly
AS
BEGIN
	SELECT * FROM @Ids
END
GO
CREATE PROCEDURE dbo.spTestNoColumnName
AS
BEGIN
	SELECT GETDATE(), 'TestResult'
END
GO
CREATE PROCEDURE dbo.spTestDate
	 @DateParam datetime2
AS
BEGIN
	SELECT @DateParam as [Date]
END
GO
CREATE PROCEDURE dbo.spBuggyProc
AS
BEGIN
	SELECT 1/CONVERT(INT, 0) AS ZeroException
END
GO
CREATE PROCEDURE dbo.spUserNotPermitted
AS
BEGIN
	RAISERROR('You are not permitted', 16,1,1);
	RETURN;
	SELECT 'hallo' AS Test
END
