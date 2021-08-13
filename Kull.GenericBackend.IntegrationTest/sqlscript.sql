CREATE TABLE dbo.Pets (PetId int PRIMARY KEY , PetName varchar(100), IsNice bit, ts timestamp)
GO
CREATE TABLE dbo.TestDbVersion(VersionNr int)
GO
INSERT INTO dbo.TestDbVersion(VersionNr) VALUES('{{DbVersion}}')
GO
INSERT INTO dbo.Pets(PetId, PetName, IsNice)
SELECT 1, 'Dog', 0
UNION ALL 
SELECT 2, 'Dog 2', 1
GO
CREATE PROCEDURE spGetPets
	@OnlyNice bit=0,
	@SearchString varchar(100)='',
	@IpAddress varchar(100)
AS
BEGIN
	SELECT PetId, PetName, IsNice, ts FROM dbo.Pets
		WHERE IsNice=1 OR @OnlyNice=0
		ORDER BY PetId;
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
CREATE PROCEDURE spUpdateDog
	@Dogid int,
	@Ts timestamp out
AS
BEGIN
	SET @Ts = 0x01;
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
GO
CREATE PROCEDURE dbo.spFile
	@Image_Content varbinary(MAX),
	@Image_ContentType varchar(1000),
	@Image_FileName varchar(1000),
	@FileDesc varchar(1000)
AS
BEGIN
	SELECT @Image_ContentType as ContentType, @Image_FileName AS [FileName], @Image_Content AS Content
END
GO
CREATE PROCEDURE dbo.spGetSomeTempTable
	@IgnoreMe bit=0,
	@AnAwesomeParam int
AS
BEGIn
	SELECT @AnAwesomeParam AS Nr INTO #out
	SELECT *FROM #out;
END
GO
CREATE PROCEDURE dbo.[Procedure with - strange name]
	@ImASpecialParameter bit
AS
BEGIn
	SELECT @ImASpecialParameter as PrmVl;
END
GO
CREATE SCHEMA tester
GO
CREATE TYPE tester.TestData AS TABLE (Id int) 
GO

CREATE PROCEDURE tester.spTestTableParam
	@Data tester.TestData READONLY
AS
BEGIn
	SELECT * FROM @Data;
END
