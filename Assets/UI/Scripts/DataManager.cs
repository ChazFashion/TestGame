using System;
using System.Data;
using Mono.Data.Sqlite;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace RacingUI
{
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        private string dbPath;
        private string connectionString;

        [Header("Debug Settings")]
        [SerializeField] private bool resetDatabaseOnStart = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeDatabase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeDatabase()
        {
            // Путь к базе данных в папке сохранений игры
            dbPath = Path.Combine(Application.persistentDataPath, "racing_game.db");
            connectionString = "URI=file:" + dbPath;

            Debug.Log($"[DataManager] Путь к базе данных: {dbPath}");

            // Для отладки: удаляем старую БД, если включен сброс
            if (resetDatabaseOnStart && File.Exists(dbPath))
            {
                File.Delete(dbPath);
                Debug.LogWarning("[DataManager] База данных сброшена (удалена) по требованию настроек!");
            }

            // Создаем таблицы, если их нет
            CreateTables();

            // Проверяем и обновляем схему существующей базы данных (миграции)
            UpgradeDatabaseSchema();
            
            // Наполняем стартовыми данными
            PopulateDefaultData();
        }

        private void UpgradeDatabaseSchema()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                string[] alterQueries = new string[]
                {
                    "ALTER TABLE AI_Profiles ADD COLUMN max_speed REAL DEFAULT 90.0;",
                    "ALTER TABLE AI_Profiles ADD COLUMN grip_boost REAL DEFAULT 1.6;",
                    "ALTER TABLE AI_Profiles ADD COLUMN downforce REAL DEFAULT 5.0;",
                    "ALTER TABLE AI_Profiles ADD COLUMN handling_offset REAL DEFAULT -0.5;",
                    "ALTER TABLE Tracks ADD COLUMN laps_count INTEGER DEFAULT 3;"
                };

                foreach (var query in alterQueries)
                {
                    try
                    {
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = query;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception)
                    {
                        // Игнорируем ошибку, если колонка уже существует в таблице
                    }
                }
                connection.Close();
            }
        }

        private void CreateTables()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    // 1. Таблица игроков
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Players (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT NOT NULL,
                            coins INTEGER DEFAULT 1000,
                            gems INTEGER DEFAULT 10,
                            current_car_id INTEGER DEFAULT 1
                        );";
                    command.ExecuteNonQuery();

                    // 2. Таблица каталога машин
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Cars_Library (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            name TEXT NOT NULL,
                            rarity TEXT NOT NULL,
                            base_price INTEGER DEFAULT 0,
                            prefab_name TEXT NOT NULL
                        );";
                    command.ExecuteNonQuery();

                    // 3. Личный гараж игрока
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Player_Garage (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            player_id INTEGER DEFAULT 1,
                            car_id INTEGER NOT NULL,
                            engine_lv INTEGER DEFAULT 1,
                            handling_lv INTEGER DEFAULT 1,
                            nitro_lv INTEGER DEFAULT 1,
                            is_unlocked INTEGER DEFAULT 0,
                            FOREIGN KEY(car_id) REFERENCES Cars_Library(id)
                        );";
                    command.ExecuteNonQuery();

                    // 4. Профили ИИ-ботов
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS AI_Profiles (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            bot_name TEXT NOT NULL,
                            skill REAL DEFAULT 0.5,
                            aggression REAL DEFAULT 0.5,
                            car_id INTEGER NOT NULL,
                            max_speed REAL DEFAULT 90.0,
                            grip_boost REAL DEFAULT 1.6,
                            downforce REAL DEFAULT 5.0,
                            handling_offset REAL DEFAULT -0.5,
                            FOREIGN KEY(car_id) REFERENCES Cars_Library(id)
                        );";
                    command.ExecuteNonQuery();

                    // 5. Трассы
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Tracks (
                            id TEXT PRIMARY KEY,
                            track_name TEXT NOT NULL,
                            is_unlocked INTEGER DEFAULT 0,
                            gold_time REAL DEFAULT 60.0,
                            laps_count INTEGER DEFAULT 3
                        );";
                    command.ExecuteNonQuery();

                    // 6. Рекорды
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Race_Records (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            player_id INTEGER DEFAULT 1,
                            track_id TEXT NOT NULL,
                            best_time REAL NOT NULL,
                            car_garage_id INTEGER NOT NULL,
                            FOREIGN KEY(track_id) REFERENCES Tracks(id),
                            FOREIGN KEY(car_garage_id) REFERENCES Player_Garage(id)
                        );";
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
            Debug.Log("[DataManager] Все таблицы успешно проверены/созданы.");
        }

        private void PopulateDefaultData()
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Создаем дефолтного игрока, если игроков вообще нет
                        long playerCount = 0;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM Players;";
                            playerCount = (long)cmd.ExecuteScalar();
                        }

                        if (playerCount == 0)
                        {
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = "INSERT INTO Players (name, coins, gems, current_car_id) VALUES ('Player 1', 1000, 10, 1);";
                                cmd.ExecuteNonQuery();
                                Debug.Log("[DataManager] Добавлен дефолтный профиль игрока.");
                            }
                        }

                        // 2. Заполняем каталог автомобилей
                        long carLibraryCount = 0;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM Cars_Library;";
                            carLibraryCount = (long)cmd.ExecuteScalar();
                        }

                        if (carLibraryCount == 0)
                        {
                            string[,] cars = new string[,] {
                                { "Zaporozhets", "Common", "0", "Car_Zapo" },
                                { "Volga", "Rare", "500", "Car_Volga" },
                                { "Skyline", "Epic", "2000", "Car_Skyline" },
                                { "CyberTruck", "Legendary", "5000", "Car_Cyber" }
                            };

                            for (int i = 0; i < cars.GetLength(0); i++)
                            {
                                using (var cmd = connection.CreateCommand())
                                {
                                    cmd.CommandText = "INSERT INTO Cars_Library (name, rarity, base_price, prefab_name) VALUES (@name, @rarity, @price, @prefab);";
                                    cmd.Parameters.AddWithValue("@name", cars[i, 0]);
                                    cmd.Parameters.AddWithValue("@rarity", cars[i, 1]);
                                    cmd.Parameters.AddWithValue("@price", int.Parse(cars[i, 2]));
                                    cmd.Parameters.AddWithValue("@prefab", cars[i, 3]);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            Debug.Log("[DataManager] Каталог автомобилей успешно наполнен.");
                        }

                        // 3. Открываем первую машину в гараже игрока
                        long garageCount = 0;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM Player_Garage;";
                            garageCount = (long)cmd.ExecuteScalar();
                        }

                        if (garageCount == 0)
                        {
                            // Разблокируем первую машину (Zaporozhets, ID=1)
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = "INSERT INTO Player_Garage (player_id, car_id, is_unlocked) VALUES (1, 1, 1);";
                                cmd.ExecuteNonQuery();
                            }
                            // Добавим остальные заблокированные машины
                            for (int id = 2; id <= 4; id++)
                            {
                                using (var cmd = connection.CreateCommand())
                                {
                                    cmd.CommandText = "INSERT INTO Player_Garage (player_id, car_id, is_unlocked) VALUES (1, @carId, 0);";
                                    cmd.Parameters.AddWithValue("@carId", id);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            Debug.Log("[DataManager] Гараж игрока успешно инициализирован.");
                        }

                        // 4. Заполняем профили ИИ-ботов
                        long botCount = 0;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM AI_Profiles;";
                            botCount = (long)cmd.ExecuteScalar();
                        }

                        if (botCount == 0)
                        {
                            // Создаем 3-х ботов с разными уровнями вождения и физическими параметрами
                            // формат: { имя, skill, aggression, car_id, max_speed, grip_boost, downforce, handling_offset }
                            string[][] bots = new string[][] {
                                new string[] { "Бот Владимир", "0.4", "0.2", "1", "80.0", "1.4", "4.0", "-0.4" }, // Легкий на Запорожце
                                new string[] { "Бот Сергей", "0.7", "0.5", "2", "95.0", "1.6", "5.0", "-0.5" },   // Средний на Волге
                                new string[] { "Бот Алекс", "0.95", "0.8", "3", "110.0", "1.8", "6.5", "-0.6" }   // Профи на Скайлайне
                            };

                            for (int i = 0; i < bots.Length; i++)
                            {
                                using (var cmd = connection.CreateCommand())
                                {
                                    cmd.CommandText = @"
                                        INSERT INTO AI_Profiles 
                                        (bot_name, skill, aggression, car_id, max_speed, grip_boost, downforce, handling_offset) 
                                        VALUES (@name, @skill, @agg, @carId, @maxSpeed, @gripBoost, @downforce, @offset);";
                                    cmd.Parameters.AddWithValue("@name", bots[i][0]);
                                    cmd.Parameters.AddWithValue("@skill", double.Parse(bots[i][1], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.Parameters.AddWithValue("@agg", double.Parse(bots[i][2], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.Parameters.AddWithValue("@carId", int.Parse(bots[i][3]));
                                    cmd.Parameters.AddWithValue("@maxSpeed", double.Parse(bots[i][4], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.Parameters.AddWithValue("@gripBoost", double.Parse(bots[i][5], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.Parameters.AddWithValue("@downforce", double.Parse(bots[i][6], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.Parameters.AddWithValue("@offset", double.Parse(bots[i][7], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            Debug.Log("[DataManager] Профили ИИ-соперников успешно созданы.");
                        }
                        else
                        {
                            // Обновляем дефолтные профили ботов новыми физическими параметрами, если они уже созданы в БД
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = @"
                                    UPDATE AI_Profiles SET max_speed = 80.0, grip_boost = 1.4, downforce = 4.0, handling_offset = -0.4 WHERE bot_name = 'Бот Владимир';
                                    UPDATE AI_Profiles SET max_speed = 95.0, grip_boost = 1.6, downforce = 5.0, handling_offset = -0.5 WHERE bot_name = 'Бот Сергей';
                                    UPDATE AI_Profiles SET max_speed = 110.0, grip_boost = 1.8, downforce = 6.5, handling_offset = -0.6 WHERE bot_name = 'Бот Алекс';";
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 5. Заполняем трассы
                        long trackCount = 0;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM Tracks;";
                            trackCount = (long)cmd.ExecuteScalar();
                        }

                        if (trackCount == 0)
                        {
                            // формат: { id, track_name, is_unlocked, gold_time, laps_count }
                            string[][] tracks = new string[][] {
                                new string[] { "Track_Forest", "Лесной Круг", "1", "55.0", "3" }, // Открыта изначально
                                new string[] { "Track_City", "Городской Спринт", "0", "42.5", "3" } // Закрыта
                            };

                            for (int i = 0; i < tracks.Length; i++)
                            {
                                using (var cmd = connection.CreateCommand())
                                {
                                    cmd.CommandText = "INSERT INTO Tracks (id, track_name, is_unlocked, gold_time, laps_count) VALUES (@id, @name, @unlocked, @gold, @laps);";
                                    cmd.Parameters.AddWithValue("@id", tracks[i][0]);
                                    cmd.Parameters.AddWithValue("@name", tracks[i][1]);
                                    cmd.Parameters.AddWithValue("@unlocked", int.Parse(tracks[i][2]));
                                    cmd.Parameters.AddWithValue("@gold", double.Parse(tracks[i][3], System.Globalization.CultureInfo.InvariantCulture));
                                    cmd.Parameters.AddWithValue("@laps", int.Parse(tracks[i][4]));
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            Debug.Log("[DataManager] Каталог трасс успешно создан.");
                        }
                        else
                        {
                            // Обновляем количество кругов по умолчанию на существующих трассах
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = @"
                                    UPDATE Tracks SET laps_count = 3 WHERE id = 'Track_Forest';
                                    UPDATE Tracks SET laps_count = 3 WHERE id = 'Track_City';";
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Debug.LogError($"[DataManager] Ошибка транзакции при наполнении данными: {ex.Message}");
                    }
                }
                connection.Close();
            }
        }

        #region ПУБЛИЧНЫЕ МЕТОДЫ ЭКОНОМИКИ И ГАРАЖА

        // Получить монеты игрока
        public int GetCoins(int playerId = 1)
        {
            int coins = 0;
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT coins FROM Players WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", playerId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        coins = Convert.ToInt32(result);
                    }
                }
            }
            return coins;
        }

        // Изменить количество монет (прибавить/вычесть)
        public bool AddCoins(int amount, int playerId = 1)
        {
            int currentCoins = GetCoins(playerId);
            if (currentCoins + amount < 0) return false; // Недостаточно средств

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Players SET coins = coins + @amount WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@id", playerId);
                    cmd.ExecuteNonQuery();
                }
            }
            Debug.Log($"[DataManager] Баланс изменен на {amount}. Текущий: {currentCoins + amount}");
            return true;
        }

        // Получить все разблокированные машины в гараже
        public List<string> GetUnlockedCarNames(int playerId = 1)
        {
            var names = new List<string>();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT cl.name FROM Player_Garage pg 
                        JOIN Cars_Library cl ON pg.car_id = cl.id 
                        WHERE pg.player_id = @pid AND pg.is_unlocked = 1;";
                    cmd.Parameters.AddWithValue("@pid", playerId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            names.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return names;
        }

        // Купить или открыть машину в гараже
        public bool UnlockCar(int carId, int playerId = 1)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Player_Garage SET is_unlocked = 1 WHERE player_id = @pid AND car_id = @cid;";
                    cmd.Parameters.AddWithValue("@pid", playerId);
                    cmd.Parameters.AddWithValue("@cid", carId);
                    int affectedRows = cmd.ExecuteNonQuery();
                    return affectedRows > 0;
                }
            }
        }

        // Получить профиль бота по ID
        public Dictionary<string, object> GetBotProfile(int botId)
        {
            var profile = new Dictionary<string, object>();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT ap.bot_name, ap.skill, ap.aggression, cl.prefab_name,
                               ap.max_speed, ap.grip_boost, ap.downforce, ap.handling_offset
                        FROM AI_Profiles ap 
                        JOIN Cars_Library cl ON ap.car_id = cl.id 
                        WHERE ap.id = @bid;";
                    cmd.Parameters.AddWithValue("@bid", botId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            profile["name"] = reader.GetString(0);
                            profile["skill"] = reader.GetDouble(1);
                            profile["aggression"] = reader.GetDouble(2);
                            profile["prefab"] = reader.GetString(3);
                            profile["max_speed"] = reader.GetDouble(4);
                            profile["grip_boost"] = reader.GetDouble(5);
                            profile["downforce"] = reader.GetDouble(6);
                            profile["handling_offset"] = reader.GetDouble(7);
                        }
                    }
                }
            }
            return profile;
        }

        // Получить профиль бота по имени
        public Dictionary<string, object> GetBotProfileByName(string name)
        {
            var profile = new Dictionary<string, object>();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT ap.bot_name, ap.skill, ap.aggression, cl.prefab_name,
                               ap.max_speed, ap.grip_boost, ap.downforce, ap.handling_offset
                        FROM AI_Profiles ap 
                        JOIN Cars_Library cl ON ap.car_id = cl.id 
                        WHERE ap.bot_name = @name;";
                    cmd.Parameters.AddWithValue("@name", name);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            profile["name"] = reader.GetString(0);
                            profile["skill"] = reader.GetDouble(1);
                            profile["aggression"] = reader.GetDouble(2);
                            profile["prefab"] = reader.GetString(3);
                            profile["max_speed"] = reader.GetDouble(4);
                            profile["grip_boost"] = reader.GetDouble(5);
                            profile["downforce"] = reader.GetDouble(6);
                            profile["handling_offset"] = reader.GetDouble(7);
                        }
                    }
                }
            }
            return profile.Count > 0 ? profile : null;
        }

        // Получить информацию о трассе
        public Dictionary<string, object> GetTrackInfo(string trackId)
        {
            var info = new Dictionary<string, object>();
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT id, track_name, is_unlocked, gold_time, laps_count FROM Tracks WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", trackId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            info["id"] = reader.GetString(0);
                            info["name"] = reader.GetString(1);
                            info["is_unlocked"] = reader.GetInt32(2) == 1;
                            info["gold_time"] = reader.GetDouble(3);
                            info["laps_count"] = reader.GetInt32(4);
                        }
                    }
                }
            }
            return info.Count > 0 ? info : null;
        }

        // Сохранить новый рекорд круга/трассы
        public bool SaveRaceRecord(string trackId, float time, int playerId = 1, int carGarageId = 1)
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                
                // Проверяем, есть ли уже рекорд для этого игрока на этой трассе
                float existingBest = float.MaxValue;
                bool hasRecord = false;

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT best_time FROM Race_Records WHERE player_id = @pid AND track_id = @tid;";
                    cmd.Parameters.AddWithValue("@pid", playerId);
                    cmd.Parameters.AddWithValue("@tid", trackId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        existingBest = Convert.ToSingle(result);
                        hasRecord = true;
                    }
                }

                // Если это новый лучший результат, сохраняем его
                if (time < existingBest)
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        if (hasRecord)
                        {
                            cmd.CommandText = "UPDATE Race_Records SET best_time = @time, car_garage_id = @carId WHERE player_id = @pid AND track_id = @tid;";
                        }
                        else
                        {
                            cmd.CommandText = "INSERT INTO Race_Records (player_id, track_id, best_time, car_garage_id) VALUES (@pid, @tid, @time, @carId);";
                        }
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.Parameters.AddWithValue("@carId", carGarageId);
                        cmd.Parameters.AddWithValue("@pid", playerId);
                        cmd.Parameters.AddWithValue("@tid", trackId);
                        cmd.ExecuteNonQuery();
                    }
                    Debug.Log($"[DataManager] Сохранен новый лучший рекорд для трассы {trackId}: {time:F2} сек!");
                    return true;
                }
                connection.Close();
            }
            return false;
        }

        #endregion
    }
}
