-- ═══════════════════════════════════════════════════════════════════════════
-- SEED: Bản dịch tiếng Anh cho tất cả POIs trong Thảo Cầm Viên
-- Chạy sau khi bảng poi_translations đã tồn tại (migration AddPoiTranslations).
--
-- Cách dùng:
--   1. Mở SQL Server Management Studio hoặc Azure Data Studio
--   2. Kết nối vào database 'web'
--   3. Chạy script này
--
-- Lưu ý:
--   - Script này dùng MERGE để có thể chạy lại nhiều lần (idempotent)
--   - Nếu bản dịch đã tồn tại → UPDATE, chưa có → INSERT
--   - poi_id PHẢI khớp với data thực trong bảng pois
-- ═══════════════════════════════════════════════════════════════════════════

-- Bước 1: Kiểm tra bảng poi_translations tồn tại
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'poi_translations')
BEGIN
    PRINT N'ERROR: Bảng poi_translations chưa tồn tại. Chạy migration trước.';
    RETURN;
END

-- Bước 2: Tạo bảng tạm chứa bản dịch EN
-- (Dùng bảng tạm để MERGE dễ dàng)
CREATE TABLE #TransEN (
    poi_id INT NOT NULL,
    name_en NVARCHAR(255) NOT NULL,
    desc_en NVARCHAR(MAX) NOT NULL
);

-- ═══════════════════════════════════════════════════════════════════════════
-- Bước 3: Chèn bản dịch cho TỪNG POI
-- ★ ĐỔI poi_id cho khớp với data thực trong DB của bạn ★
-- ═══════════════════════════════════════════════════════════════════════════

-- Nếu bạn chưa biết poi_id, chạy:  SELECT poi_id, name FROM pois ORDER BY poi_id;
-- Rồi thay số bên dưới cho đúng.

INSERT INTO #TransEN (poi_id, name_en, desc_en) VALUES
-- Khu vực chính
(1,  N'Saigon Zoo & Botanical Gardens',
     N'Founded in 1864, the Saigon Zoo and Botanical Gardens is one of the oldest zoos in the world. Located in the heart of Ho Chi Minh City, it houses over 1,300 animals of 125 species and more than 2,000 trees of 360 species.'),

(2,  N'Elephant Enclosure',
     N'Home to Asian elephants, one of the most iconic species at the zoo. Asian elephants are classified as endangered by the IUCN Red List.'),

(3,  N'Tiger Habitat',
     N'The tiger habitat houses Indochinese tigers, a critically endangered subspecies. Only about 350 remain in the wild across Southeast Asia.'),

(4,  N'Giraffe Area',
     N'Meet our giraffes — the tallest living terrestrial animals. They can reach heights of up to 5.7 meters and their tongues can be 45 cm long.'),

(5,  N'Hippopotamus Pool',
     N'Observe the hippos in their semi-aquatic habitat. Despite their gentle appearance, hippos are considered one of the most dangerous animals in Africa.'),

(6,  N'Reptile House',
     N'Explore the fascinating world of reptiles including pythons, cobras, crocodiles, and rare turtles native to Vietnam.'),

(7,  N'Bird Aviary',
     N'A lush walk-through aviary featuring tropical birds, hornbills, peacocks, and endangered species from Vietnam''s forests.'),

(8,  N'Primate Zone',
     N'Home to various primate species including gibbons, langurs, and macaques. Many are endemic to Vietnam and critically endangered.'),

(9,  N'Botanical Garden',
     N'Stroll through the historic botanical garden with over 2,000 trees representing 360 species, including century-old specimens from the French colonial era.'),

(10, N'Children''s Playground',
     N'A safe and fun area designed for young visitors with educational animal exhibits and interactive activities.');

-- ═══════════════════════════════════════════════════════════════════════════
-- Bước 4: MERGE — Upsert vào poi_translations
-- ═══════════════════════════════════════════════════════════════════════════
MERGE poi_translations AS target
USING (
    SELECT t.poi_id, 'en' AS language_code, t.name_en AS [name], t.desc_en AS [description]
    FROM #TransEN t
    -- Chỉ insert cho các poi_id thực sự tồn tại trong bảng pois
    INNER JOIN pois p ON p.poi_id = t.poi_id
) AS source
ON target.poi_id = source.poi_id AND target.language_code = source.language_code
WHEN MATCHED THEN
    UPDATE SET
        target.[name] = source.[name],
        target.[description] = source.[description]
WHEN NOT MATCHED THEN
    INSERT (poi_id, language_code, [name], [description])
    VALUES (source.poi_id, source.language_code, source.[name], source.[description]);

-- Cleanup
DROP TABLE #TransEN;

-- Kiểm tra kết quả
SELECT
    p.poi_id,
    p.[name] AS name_vi,
    t.[name] AS name_en,
    LEFT(t.[description], 60) AS desc_en_preview
FROM pois p
LEFT JOIN poi_translations t ON t.poi_id = p.poi_id AND t.language_code = 'en'
ORDER BY p.poi_id;

PRINT N'✓ Seed EN translations hoàn tất. Chạy lại script này bất cứ lúc nào để cập nhật.';
GO
