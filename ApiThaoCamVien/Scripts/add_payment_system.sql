-- =============================================================================
--  add_payment_system.sql
--
--  Manual fallback script: thêm hệ thống thanh toán Premium vào database.
--  Dùng khi không thể chạy `dotnet ef database update` (vd: máy chỉ có SSMS).
--
--  Idempotent: chạy nhiều lần không lỗi (kiểm tra IF NOT EXISTS).
-- =============================================================================

USE [web];
GO

-- 1. Thêm cột is_premium + premium_price vào bảng pois
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'is_premium' AND Object_ID = Object_ID(N'pois'))
BEGIN
    ALTER TABLE [pois] ADD [is_premium] BIT NOT NULL DEFAULT 0;
    PRINT '✓ Added column pois.is_premium';
END
ELSE PRINT '· pois.is_premium already exists';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'premium_price' AND Object_ID = Object_ID(N'pois'))
BEGIN
    ALTER TABLE [pois] ADD [premium_price] DECIMAL(18,0) NULL;
    PRINT '✓ Added column pois.premium_price';
END
ELSE PRINT '· pois.premium_price already exists';
GO

-- 2. Bảng payment_transactions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'payment_transactions')
BEGIN
    CREATE TABLE [payment_transactions] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [TransactionCode] NVARCHAR(50)  NOT NULL,
        [PoiId]           INT           NOT NULL,
        [SessionId]       NVARCHAR(100) NOT NULL,
        [DeviceId]        NVARCHAR(200) NOT NULL,
        [Amount]          DECIMAL(18,0) NOT NULL,
        [Currency]        NVARCHAR(3)   NOT NULL DEFAULT 'VND',
        [PaymentMethod]   NVARCHAR(20)  NOT NULL DEFAULT 'vnpay',
        [Status]          NVARCHAR(20)  NOT NULL DEFAULT 'pending',
        [VnPayUrl]        NVARCHAR(MAX) NULL,
        [QrExpiredAt]     DATETIME2     NULL,
        [GatewayRef]      NVARCHAR(200) NULL,
        [GatewayResponse] NVARCHAR(MAX) NULL,
        [FailureReason]   NVARCHAR(500) NULL,
        [CreatedAt]       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt]     DATETIME2     NULL,
        CONSTRAINT [PK_payment_transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_txn_code] UNIQUE ([TransactionCode]),
        CONSTRAINT [FK_payment_poi] FOREIGN KEY ([PoiId]) REFERENCES [pois]([poi_id])
    );

    CREATE INDEX [IX_txn_device_poi] ON [payment_transactions] ([DeviceId], [PoiId]);
    CREATE INDEX [IX_payment_transactions_PoiId] ON [payment_transactions] ([PoiId]);
    PRINT '✓ Created table payment_transactions';
END
ELSE PRINT '· payment_transactions already exists';
GO

-- 3. Bảng premium_accesses
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'premium_accesses')
BEGIN
    CREATE TABLE [premium_accesses] (
        [Id]            INT IDENTITY(1,1) NOT NULL,
        [TransactionId] INT           NOT NULL,
        [PoiId]         INT           NOT NULL,
        [SessionId]     NVARCHAR(100) NOT NULL,
        [DeviceId]      NVARCHAR(200) NOT NULL,
        [GrantedAt]     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [ExpiresAt]     DATETIME2     NULL,
        CONSTRAINT [PK_premium_accesses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_access_transaction] FOREIGN KEY ([TransactionId]) REFERENCES [payment_transactions]([Id]),
        CONSTRAINT [FK_access_poi] FOREIGN KEY ([PoiId]) REFERENCES [pois]([poi_id])
    );

    CREATE INDEX [IX_access_device_poi] ON [premium_accesses] ([DeviceId], [PoiId]);
    CREATE INDEX [IX_premium_accesses_PoiId] ON [premium_accesses] ([PoiId]);
    CREATE INDEX [IX_premium_accesses_TransactionId] ON [premium_accesses] ([TransactionId]);
    PRINT '✓ Created table premium_accesses';
END
ELSE PRINT '· premium_accesses already exists';
GO

-- 4. Demo data: đánh dấu vài POI là premium (50.000 VND)
UPDATE [pois]
SET [is_premium] = 1, [premium_price] = 50000
WHERE [poi_id] IN (1, 5, 9) AND [is_premium] = 0;

PRINT '✓ Seeded sample premium POIs (id 1, 5, 9 = 50.000 VND)';
GO

PRINT '====================================================';
PRINT '✅ HOÀN TẤT: Database đã sẵn sàng cho hệ thống thanh toán';
PRINT '====================================================';
