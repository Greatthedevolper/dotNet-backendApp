using System;
using System.Collections.Generic;
using BCrypt.Net;
using DotNetApi.Models;
using MySqlConnector;
using Org.BouncyCastle.Asn1.Ocsp;

namespace DotNetApi.Data
{
    public class CategoryRepository
    {
        private readonly Database _database;
        private readonly UserRepository _userRepository;

        public CategoryRepository(UserRepository userRepository)
        {
            _database = new Database();
            _userRepository = userRepository;
        }

        public List<Category> GetAllCategories(string search, HttpRequest request)
        {
            List<Category> categories = [];

            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query =
                        @"
                SELECT * FROM categories
                WHERE (@Search IS NULL OR @Search = '' OR  name LIKE @Search OR `description` LIKE @Search OR `slug` LIKE @Search)";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@Search", $"%{search}%");

                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        categories.Add(
                            new Category
                            {
                                Id = reader.GetInt32("id"),
                                Name = reader.GetString("name"),
                                Description = reader.IsDBNull(reader.GetOrdinal("description"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("description")),
                                Slug = reader.GetString("slug"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                UpdatedAt = reader.GetDateTime("updated_at"),
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error: " + ex.Message);
                }
            }

            return categories;
        }

        public Category? GetSingleCategory(int id)
        {
            Category? category = null;
            using (MySqlConnection conn = _database.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM categories WHERE id=@id LIMIT 1";
                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using MySqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    category = new Category
                    {
                        Id = reader.GetInt32("id"),
                        Name = reader.GetString("name"),
                        Description = reader.IsDBNull(reader.GetOrdinal("description"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("description")),
                        Slug = reader.GetString("slug"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at"),
                    };
                }
            }

            return category;
        }

        public bool SaveCategory(string? categoryName = null, string? categoryDescription = null)
        {
            using MySqlConnection conn = _database.GetConnection();
            conn.Open();
            string insertQuery =
                @"INSERT INTO categories (name,description,slug, created_at, updated_at) VALUES (@name,@description, @slug, NOW(), NOW());";

            using MySqlCommand cmd = new(insertQuery, conn);
            cmd.Parameters.AddWithValue("@name", categoryName);
            cmd.Parameters.AddWithValue("@description", categoryDescription);
            cmd.Parameters.AddWithValue("@slug", "/" + categoryName);

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteCategory(int id)
        {
            using MySqlConnection conn = _database.GetConnection();
            conn.Open();
            string approvalQuery = "DELETE FROM listings WHERE id = @id;";
            using MySqlCommand cmd = new(approvalQuery, conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
