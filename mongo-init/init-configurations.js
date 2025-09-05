// Örnek konfigürasyon verileri için MongoDB başlatma scripti
// Bu script, MongoDB konteyneri ilk kez başladığında çalışır

db = db.getSiblingDB('ConfigurationDb');

// Örnek verilerle konfigürasyon koleksiyonu oluştur
db.Configurations.insertMany([
    {
        "_id": ObjectId(),
        "name": "SiteName",
        "type": 0, // String
        "value": "soty.io",
        "isActive": true,
        "applicationName": "SERVICE-A",
        "createdAt": new Date(),
        "updatedAt": new Date()
    },
    {
        "_id": ObjectId(),
        "name": "IsBasketEnabled",
        "type": 3, // Bool
        "value": "true",
        "isActive": true,
        "applicationName": "SERVICE-B",
        "createdAt": new Date(),
        "updatedAt": new Date()
    },
    {
        "_id": ObjectId(),
        "name": "MaxItemCount",
        "type": 1, // Int
        "value": "50",
        "isActive": false,
        "applicationName": "SERVICE-A",
        "createdAt": new Date(),
        "updatedAt": new Date()
    }
]);

// Daha iyi performans için indeksler oluştur
db.Configurations.createIndex({ "applicationName": 1, "name": 1, "isActive": 1 });
db.Configurations.createIndex({ "applicationName": 1 });
db.Configurations.createIndex({ "isActive": 1 });
db.Configurations.createIndex({ "name": 1 });


print("Konfigürasyon verileri başarıyla başlatıldı!");
print(`Toplam eklenen konfigürasyon sayısı: ${db.Configurations.countDocuments()}`);


