# Multi-Tenant POS Microservice - Setup Guide

## 🚀 Quick Start (5 Minutes)

### Prerequisites

Before running this project, ensure you have:

1. ✅ **.NET 8 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
2. ✅ **SQL Server** (LocalDB, Express, or Developer Edition)
   - LocalDB: Comes with Visual Studio
   - Express: [Download here](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
3. ✅ **Docker Desktop** - [Download here](https://www.docker.com/products/docker-desktop/)
   - Required for running Redis
4. ✅ **Visual Studio 2022** (recommended) or VS Code

---

## 📦 Step 1: Clone & Restore Packages

```bash
# Clone the repository (or download ZIP)
cd MicroservicesBase

# Restore NuGet packages
dotnet restore
```

---

## 🐳 Step 2: Start Redis (Docker)

Redis is used for caching tenant connection strings to reduce Master DB load.

### Option A: Using Docker Command (Windows/Mac/Linux)

```bash
# Start Redis container
docker run -d -p 6379:6379 --name redis-template redis:latest

# Verify it's running
docker ps
```

### Option B: Using Docker Desktop GUI

1. Open Docker Desktop
2. Search for `redis` in the Images tab
3. Click **Pull** to download the Redis image
4. Click **Run** and configure:
   - **Container name:** `redis-template`
   - **Port mapping:** `6379:6379`
5. Click **Run**

### Verify Redis is Running

```bash
# Check if Redis is responding
docker exec -it redis-template redis-cli ping
# Expected output: PONG
```

### Common Redis Commands

```bash
# Stop Redis
docker stop redis-template

# Start Redis (if already created)
docker start redis-template

# Remove Redis container
docker rm -f redis-template
```

---

## 🗄️ Step 3: Set Up Databases

### 3.1 Create Master Database

Run this SQL script on your SQL Server instance:

```sql
-- Create Master Database
CREATE DATABASE TenantMaster;
GO

USE TenantMaster;
GO

-- Create Tenants table
CREATE TABLE dbo.Tenants(
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    ConnectionString NVARCHAR(500) NOT NULL,
    IsActive BIT NOT NULL DEFAULT(1)
);
GO

-- Insert sample tenants
INSERT INTO dbo.Tenants (Name, ConnectionString, IsActive)
VALUES 
    ('7ELEVEN', 'Server=YOUR_SERVER_NAME;Database=Sales7Eleven;Integrated Security=True;TrustServerCertificate=True;', 1),
    ('BURGERKING', 'Server=YOUR_SERVER_NAME;Database=SalesBurgerKing;Integrated Security=True;TrustServerCertificate=True;', 1);
GO
```

**⚠️ Important:** Replace `YOUR_SERVER_NAME` with your actual SQL Server instance name:
- LocalDB: `(localdb)\MSSQLLocalDB`
- SQL Server Express: `.\SQLEXPRESS` or `localhost\SQLEXPRESS`
- SQL Server Developer: `localhost` or `.`

### 3.2 Create Tenant Databases

Run this script for **each tenant** (7-Eleven example):

```sql
-- Create 7-Eleven database
CREATE DATABASE Sales7Eleven;
GO

USE Sales7Eleven;
GO

-- Create Sales table
CREATE TABLE dbo.Sales (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TenantId NVARCHAR(100) NOT NULL,
    StoreId NVARCHAR(50) NOT NULL,
    RegisterId NVARCHAR(50) NOT NULL,
    ReceiptNumber NVARCHAR(50) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    NetTotal DECIMAL(18,2) NOT NULL,
    TaxTotal DECIMAL(18,2) NOT NULL,
    GrandTotal DECIMAL(18,2) NOT NULL
);
GO

CREATE UNIQUE INDEX UX_Sales_Receipt ON dbo.Sales(StoreId, RegisterId, ReceiptNumber);
GO

-- Create SaleItems table
CREATE TABLE dbo.SaleItems (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SaleId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.Sales(Id),
    Sku NVARCHAR(50) NOT NULL,
    Qty INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL
);
GO

CREATE INDEX IX_SaleItems_SaleId ON dbo.SaleItems(SaleId);
GO

-- Create stored procedure for reading sales
CREATE PROCEDURE dbo.GetSaleWithItems
    @SaleId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Return sale header
    SELECT 
        Id, TenantId, StoreId, RegisterId, ReceiptNumber, 
        CreatedAt, NetTotal, TaxTotal, GrandTotal
    FROM dbo.Sales WITH (NOLOCK)
    WHERE Id = @SaleId;
    
    -- Return sale items
    SELECT Sku, Qty, UnitPrice
    FROM dbo.SaleItems WITH (NOLOCK)
    WHERE SaleId = @SaleId;
END
GO

-- Insert sample data
INSERT INTO dbo.Sales (Id, TenantId, StoreId, RegisterId, ReceiptNumber, CreatedAt, NetTotal, TaxTotal, GrandTotal)
VALUES 
    ('11111111-1111-1111-1111-111111111111', '7ELEVEN', 'STORE001', 'REG01', 'RCP-7E-001', 
     SYSDATETIMEOFFSET(), 20.00, 1.20, 21.20);
GO

INSERT INTO dbo.SaleItems (SaleId, Sku, Qty, UnitPrice)
VALUES 
    ('11111111-1111-1111-1111-111111111111', 'SKU-COFFEE', 1, 10.00),
    ('11111111-1111-1111-1111-111111111111', 'SKU-SANDWICH', 1, 10.00);
GO
```

**Repeat this script** for `SalesBurgerKing` database (change database name and sample data accordingly).

---

## ⚙️ Step 4: Configure Connection Strings

Open `MicroservicesBase.API/appsettings.json` and update:

```json
{
  "ConnectionStrings": {
    "TenantMaster": "Server=YOUR_SERVER_NAME;Database=TenantMaster;Integrated Security=True;TrustServerCertificate=True;",
    "Redis": "localhost:6379"
  }
}
```

**Replace `YOUR_SERVER_NAME`** with your SQL Server instance:
- LocalDB: `(localdb)\\MSSQLLocalDB`
- SQL Express: `.\\SQLEXPRESS`
- SQL Server: `localhost` or `.`

---

## ▶️ Step 5: Run the Application

### Option A: Visual Studio

1. Open `MicroservicesBase.sln` in Visual Studio 2022
2. Set `MicroservicesBase.API` as the startup project (right-click → Set as Startup Project)
3. Press **F5** to run with debugging (or Ctrl+F5 without debugging)
4. Swagger UI will open automatically at `https://localhost:60304/swagger`

### Option B: Command Line

```bash
cd MicroservicesBase.API
dotnet run
```

The API will start on:
- HTTPS: `https://localhost:60304`
- HTTP: `http://localhost:60305`

Open Swagger: `https://localhost:60304/swagger`

---

## ✅ Step 6: Verify Everything Works

### Test Health Checks

Open these URLs in your browser:

1. **Liveness:** `https://localhost:60304/health/live`
   - Should return `200 OK` with `"status": "Healthy"`

2. **Readiness:** `https://localhost:60304/health/ready`
   - Should return `200 OK` if Master DB and Redis are reachable

3. **Tenant Check:** `https://localhost:60304/health/tenant/7ELEVEN`
   - Should return `200 OK` if 7-Eleven database is accessible

### Test Business Endpoints

1. Open Swagger UI: `https://localhost:60304/swagger`
2. Navigate to **Api** → **GET /api/sales/{id}**
3. Click **Try it out**
4. Enter ID: `11111111-1111-1111-1111-111111111111`
5. Add Header: `X-Tenant: 7ELEVEN`
6. Click **Execute**

**Expected Response:**
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "tenantId": "7ELEVEN",
  "storeId": "STORE001",
  "registerId": "REG01",
  "receiptNumber": "RCP-7E-001",
  "netTotal": 20.00,
  "taxTotal": 1.20,
  "grandTotal": 21.20,
  "items": [
    { "sku": "SKU-COFFEE", "qty": 1, "unitPrice": 10.00 },
    { "sku": "SKU-SANDWICH", "qty": 1, "unitPrice": 10.00 }
  ]
}
```

---

## 🐛 Troubleshooting

### Redis Connection Errors

**Error:** `"RedisConnectionException: It was not possible to connect to the redis server(s)"`

**Solution:**
1. Check if Redis is running: `docker ps`
2. If not running: `docker start redis-template`
3. Verify Redis responds: `docker exec -it redis-template redis-cli ping`
4. Check connection string in `appsettings.json`: `"Redis": "localhost:6379"`

### SQL Server Connection Errors

**Error:** `"A network-related or instance-specific error occurred"`

**Solution:**
1. Verify SQL Server is running (check Services or SQL Server Configuration Manager)
2. Test connection with SSMS (SQL Server Management Studio)
3. Update `appsettings.json` with correct server name
4. Ensure `TrustServerCertificate=True` is in connection string

### Tenant Not Found

**Error:** `"No tenant found with name 'XXX'"`

**Solution:**
1. Check `TenantMaster.dbo.Tenants` table has the tenant
2. Verify you're using the **Name** field (e.g., "7ELEVEN"), not the GUID Id
3. Tenant names are **case-sensitive** in the database query

### Port Already in Use

**Error:** `"Address already in use"`

**Solution:**
1. Check `launchSettings.json` for port configuration
2. Kill any existing dotnet.exe processes: `taskkill /F /IM dotnet.exe`
3. Change ports in `launchSettings.json` if needed

---

## 🔧 Advanced Configuration

### Changing Redis Port

If port `6379` is already in use:

```bash
# Run Redis on a different port (e.g., 6380)
docker run -d -p 6380:6379 --name redis-template redis:latest
```

Update `appsettings.json`:
```json
"Redis": "localhost:6380"
```

### Using Remote SQL Server

If using a remote SQL Server (e.g., Azure SQL):

```json
"ConnectionStrings": {
  "TenantMaster": "Server=your-server.database.windows.net;Database=TenantMaster;User Id=admin;Password=YourPassword;TrustServerCertificate=True;"
}
```

Update tenant connection strings in the `Tenants` table accordingly.

### Disabling HTTPS (for testing only)

In `launchSettings.json`, remove the HTTPS profile or use the HTTP URL.

⚠️ **Never disable HTTPS in production!**

---

## 📚 Next Steps

Once the system is running:

1. ✅ Read `README.md` for architecture overview
2. ✅ Check `GapAnalysis.md` for roadmap and missing features
3. ✅ Review `AnalysisDocument.md` for detailed technical analysis
4. 🚀 Start implementing Sprint 1 features (Performance Optimization, Audit Logging)

---

## 🆘 Need Help?

If you encounter issues:

1. Check the `Logs/` folder for detailed error messages
2. Look at the console output in Visual Studio
3. Verify all prerequisites are installed
4. Ensure Redis is running: `docker ps`
5. Confirm databases exist and connection strings are correct

---

## 🎉 Success Checklist

You've successfully set up the project if:

- ✅ Redis is running in Docker
- ✅ `TenantMaster` database exists with 2+ tenants
- ✅ Tenant databases exist (Sales7Eleven, SalesBurgerKing)
- ✅ `/health/ready` returns `200 OK` with all checks healthy
- ✅ `/api/sales/{id}` with `X-Tenant: 7ELEVEN` header returns sale data
- ✅ Swagger UI shows all endpoints grouped by **Api** and **Health**

**Congratulations! Your multi-tenant POS microservice is running!** 🚀

