using SQLite;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        private async Task InitAsync()
        {
            if (_database != null) return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ThaoCamVien.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            await _database.CreateTableAsync<Poi>();
        }

        public async Task SeedDataAsync()
        {
            await InitAsync();

            var count = await _database.Table<Poi>().CountAsync();
            if (count == 0)
            {
                // Bơm dữ liệu chuẩn xác lấy từ file SQL của bạn
                var mockPois = new List<Poi>
                {
                    new Poi { PoiId = 1, CategoryId = 1, Name = "Khu Hổ Đông Dương", Description = "Nơi nuôi hổ Đông Dương", Latitude = 10.78723000m, Longitude = 106.70589000m, Radius = 15, Priority = 1, IsActive = true, CreatedAt = DateTime.Now },
                    new Poi { PoiId = 2, CategoryId = 1, Name = "Khu Sư Tử", Description = "Nơi nuôi sư tử châu Phi", Latitude = 10.78741000m, Longitude = 106.70612000m, Radius = 15, Priority = 1, IsActive = true, CreatedAt = DateTime.Now },
                    new Poi { PoiId = 3, CategoryId = 1, Name = "Khu Voi", Description = "Nơi nuôi voi châu Á", Latitude = 10.78752000m, Longitude = 106.70630000m, Radius = 15, Priority = 1, IsActive = true, CreatedAt = DateTime.Now },
                    new Poi { PoiId = 9, CategoryId = 2, Name = "Vườn Lan", Description = "Nơi trồng nhiều loại lan quý", Latitude = 10.78812000m, Longitude = 106.70720000m, Radius = 15, Priority = 1, IsActive = true, CreatedAt = DateTime.Now }
                };

                await _database.InsertAllAsync(mockPois);
                Console.WriteLine("Đã bơm dữ liệu PoI thành công vào SQLite!");
            }
        }

        public async Task<List<Poi>> GetAllPoisAsync()
        {
            await InitAsync();
            return await _database.Table<Poi>().ToListAsync();
        }
    }
}