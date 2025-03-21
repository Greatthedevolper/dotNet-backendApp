using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using DotNetApi.Models;  // ✅ Ensure correct reference for User
using BCrypt.Net;
using DotNetApi.Services;

namespace DotNetApi.Data
{
    public class UserRepository  // ✅ Now it's public
    {
        private readonly Database _database;
        private readonly EmailService _emailService;

        public UserRepository(EmailService emailService)
        {
            _database = new Database();
            _emailService = emailService;
        }

        public List<User> GetAllUsers()
        {
            List<User> users = new List<User>();

            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query = "SELECT id, name, email, role FROM users WHERE role = 'user';";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new User
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    Email = reader.GetString("email"),
                                    Role = reader.GetString("role")
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error: " + ex.Message);
                }
            }
            return users;
        }
        public User? AddUser(string name, string email, string password)
        {
            using MySqlConnection conn = _database.GetConnection();
            try
            {
                conn.Open();
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                string verificationToken = Guid.NewGuid().ToString();

                string query = @"INSERT INTO users 
                        (name, email, role, email_verified_at, password, remember_token, created_at, updated_at) 
                        VALUES 
                        (@name, @email, @role, NULL, @password, @remember, NOW(), NOW());
                        SELECT LAST_INSERT_ID();";

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@role", "user");
                cmd.Parameters.AddWithValue("@remember", verificationToken);
                cmd.Parameters.AddWithValue("@password", hashedPassword);

                object? insertedId = cmd.ExecuteScalar();
                if (insertedId != null && int.TryParse(insertedId.ToString(), out int userId))
                {
                    var user = new User
                    {
                        Id = userId,
                        Name = name,
                        Email = email,
                        Password = hashedPassword,
                        VerificationToken = verificationToken
                    };

                    // Send Verification Email
                    Task.Run(() => _emailService.SendEmailAsync(email, "Verify Your Email",
                        $"Click the link to verify: http://localhost:4000/guest/verify?token={verificationToken}&email={email}"));

                    return user;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error inserting user: {ex.Message}");
            }

            return null;
        }

        public User? GetUserByEmail(string email)
        {
            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query = "SELECT id, name, email, role, email_verified_at  FROM users WHERE email = @email LIMIT 1;";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@email", email);

                    using MySqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read()) // If user is found
                    {
                        return new User
                        {
                            Id = reader.GetInt32("id"),
                            Name = reader.GetString("name"),
                            Email = reader.GetString("email"),
                            Role = reader.GetString("role"),
                            EmailVerifiedAt = reader.IsDBNull(reader.GetOrdinal("email_verified_at"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("email_verified_at"))
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error: " + ex.Message);
                }
            }

            return null; // Return null if no user is found
        }
        public bool VerifyPassword(string email, string enteredPassword)
        {
            using MySqlConnection conn = _database.GetConnection();
            try
            {
                conn.Open();
                string query = "SELECT password FROM users WHERE email = @email;";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@email", email);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedHashedPassword = reader.GetString("password");

                            // Verify entered password with stored hash
                            return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHashedPassword);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error verifying password: " + ex.Message);
            }
            return false;
        }
        public bool VerifyUserEmail(string email, string token)
        {
            using MySqlConnection conn = _database.GetConnection();
            try
            {
                conn.Open();

                // First, check if the token matches the email
                string checkQuery = "SELECT id FROM users WHERE email = @Email AND remember_token = @Token LIMIT 1;";
                using (MySqlCommand checkCmd = new(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Email", email);
                    checkCmd.Parameters.AddWithValue("@Token", token);

                    object result = checkCmd.ExecuteScalar();
                    if (result == null)
                    {
                        return false; // No matching user found
                    }
                }

                // Update the email_verified_at field if token matches
                string updateQuery = "UPDATE users SET email_verified_at = NOW(), remember_token = NULL WHERE email = @Email AND remember_token = @Token;";
                using (MySqlCommand cmd = new(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Token", token);

                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error verifying email: " + ex.Message);
                return false;
            }
        }


    }
}

