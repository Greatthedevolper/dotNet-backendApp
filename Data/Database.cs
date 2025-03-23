
using System;
using MySql.Data.MySqlClient;

namespace DotNetApi.Data
{
    class Database
    {
        private string connectionString = "server=localhost;database=listing_dotnet;user=root;password='';";

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(connectionString);
        }

        public void TestConnection()
        {
            using (MySqlConnection conn = GetConnection())
            {
                try
                {
                    conn.Open();
                    Console.WriteLine("✅ Database Connected Successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Connection Failed: " + ex.Message);
                }
            }
        }
    }
}
