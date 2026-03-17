// 1. Khởi tạo bản đồ
const map = L.map('map');

// 2. Nền bản đồ (Số 1 Dương chọn)
L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
    attribution: '© CARTO'
}).addTo(map);

async function initZooMap() {
    try {
        // --- PHẦN 1: LẤY VÀ VẼ RANH GIỚI ĐEN ---
        const boundaryResponse = await fetch('https://localhost:7208/api/map/boundary');
        const boundaryData = await boundaryResponse.json();

        // Chuyển dữ liệu từ API thành mảng tọa độ Leaflet hiểu được
        const latLngs = boundaryData.map(point => [point.lat, point.lng]);

        const boundaryLine = L.polygon(latLngs, {
            color: 'black',      // Đường kẻ màu đen bao quanh
            weight: 3,           // Độ dày đường kẻ
            fillColor: 'black',
            fillOpacity: 0.05,   // Màu nền cực nhạt bên trong
            interactive: false
        }).addTo(map);

        // Tự động căn bản đồ theo ranh giới
        map.fitBounds(boundaryLine.getBounds());

        // --- PHẦN 2: LẤY VÀ VẼ 18 CHUỒNG THÚ (Dữ liệu cũ) ---
        const poisResponse = await fetch('https://localhost:7208/api/map/pois');
        const pois = await poisResponse.json();

        pois.forEach(poi => {
            L.circle([poi.latitude, poi.longitude], {
                color: 'red',
                fillColor: '#f03',
                fillOpacity: 0.4,
                radius: poi.radius || 15
            }).addTo(map).bindPopup(`<b>${poi.name}</b>`);
        });

    } catch (error) {
        console.error("Lỗi:", error);
    }
}

initZooMap();