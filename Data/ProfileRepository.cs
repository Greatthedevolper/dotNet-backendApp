using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using DotNetApi.Models;
using DotNetApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotNetApi.Data
{
    public class ProfileRepository(EmailService emailService)
    {
        private readonly Database _database = new();
        private readonly EmailService _emailService = emailService;

        public bool UpdateProfileImage(int userId, string filePath)
        {
            if (userId <= 0) // Validate user ID
            {
                return false;
            }

            try
            {
                using var conn = _database.GetConnection();
                conn.Open();
                string query = "UPDATE users SET profile_picture = @filePath WHERE id = @id";

                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@filePath", filePath);
                cmd.Parameters.AddWithValue("@id", userId);
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Database Error: " + ex.Message);
                return false;
            }
        }
        public string? GetExistingProfileImagePath(int userId)
        {
            try
            {
                using var conn = _database.GetConnection();
                conn.Open();

                string query = "SELECT profile_picture FROM users WHERE id = @id LIMIT 1";
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", userId);

                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? Convert.ToString(result) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error retrieving image path: " + ex.Message);
                return null;
            }
        }

        public bool UpdateProfile(int Id, string Email, string Name)
        {
            try
            {
                using var conn = _database.GetConnection();
                conn.Open();
                string query = "UPDATE users SET name=@name,email=@email WHERE id=@id ";
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", Id);
                cmd.Parameters.AddWithValue("@email", Email);
                cmd.Parameters.AddWithValue("@name", Name);
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected == 0)
                {
                    return false;
                }
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Database Error" + ex.Message);
            }
            return true;
        }
    }
}
