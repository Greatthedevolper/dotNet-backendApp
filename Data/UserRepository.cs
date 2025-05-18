using System;
using System.Collections.Generic;
using MySqlConnector;
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

                    // Send Account Verification Email

                    Task.Run(() => _emailService.SendEmailAsync(email, "Verify Your Email",
            $@"<p style='font-size: 16px; color: #333;'>Click the link below to reset your password:</p>
                <p><a href='http://localhost:4000/guest/verify?token={verificationToken}&email={email}'
                    style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: #ffffff; 
                    text-decoration: none; font-size: 16px; border-radius: 5px;' target='_blank'>Verify your account</a></p>
                <p>If you didn't request a password reset, you can ignore this email.</p>"));

                    return user;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error inserting user: {ex.Message}");
            }

            return null;
        }

        public User? GetUserByEmail(string email, HttpRequest request)
        {
            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query = "SELECT id, name, email, role, email_verified_at, remember_token, profile_picture  FROM users WHERE email = @email LIMIT 1;";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@email", email);

                    using MySqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read()) // If user is found
                    {
                        string baseUrl = $"{request.Scheme}://{request.Host}";
                        string profilePicturePath = reader.IsDBNull(reader.GetOrdinal("profile_picture")) ? "" : reader.GetString("profile_picture");
                        string physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", profilePicturePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                        string profilePicUrl;
                        if (!string.IsNullOrEmpty(profilePicturePath) && System.IO.File.Exists(physicalPath))
                        {
                            profilePicUrl = $"{baseUrl}{profilePicturePath}";
                        }
                        else
                        {

                            profilePicUrl = $"{baseUrl}/uploads/profile_pictures/default-avatar.jpeg";
                        }


                        // string? imagePath = reader.IsDBNull(reader.GetOrdinal("profile_picture")) ? null : reader.GetString("profile_picture");

                        // string profilePicUrl = string.IsNullOrWhiteSpace(imagePath)
                        //     ? $"{baseUrl}/uploads/profile_pictures/default-avatar.jpeg"
                        //     : $"{baseUrl}/{imagePath}";
                        return new User
                        {
                            Id = reader.GetInt32("id"),
                            Name = reader.GetString("name"),
                            Email = reader.GetString("email"),
                            Role = reader.GetString("role"),
                            Token = reader.IsDBNull(reader.GetOrdinal("remember_token"))
                        ? null : reader.GetString("remember_token"),
                            ProfilePicture = profilePicUrl,
                            EmailVerifiedAt = reader.IsDBNull(reader.GetOrdinal("email_verified_at"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("email_verified_at"))
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error11: " + ex.Message);
                }
            }

            return null; // Return null if no user is found
        }
        public User? GetUserById(int Id)
        {
            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query = "SELECT id, name, email, role, email_verified_at, remember_token, profile_picture  FROM users WHERE id = @id LIMIT 1;";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@id", Id);

                    using MySqlDataReader reader = cmd.ExecuteReader();
                    if (reader.Read()) // If user is found
                    {
                        return new User
                        {
                            Id = reader.GetInt32("id"),
                            Name = reader.GetString("name"),
                            Email = reader.GetString("email"),
                            Role = reader.GetString("role"),
                            Token = reader.IsDBNull(reader.GetOrdinal("remember_token"))
                        ? null : reader.GetString("remember_token"),
                            ProfilePicture = reader.IsDBNull(reader.GetOrdinal("profile_picture"))
                        ? null : reader.GetString("profile_picture"),
                            EmailVerifiedAt = reader.IsDBNull(reader.GetOrdinal("email_verified_at"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("email_verified_at"))
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error11: " + ex.Message);
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

                    object? result = checkCmd.ExecuteScalar();
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

        public bool SendResetPasswordEmail(string email)
        {
            string verificationToken = Guid.NewGuid().ToString();

            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    string query = "Update users set remember_token = @token WHERE email = @email";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@token", verificationToken);
                    cmd.Parameters.AddWithValue("@email", email);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    if (rowsAffected == 0)
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error: " + ex.Message);
                    return false;
                }
            }
            Task.Run(() => _emailService.SendEmailAsync(email, "Reset Your Password",
                $@"<p style='font-size: 16px; color: #333;'>Click the link below to reset your password:</p>
                <p><a href='http://localhost:4000/guest/reset-password?token={verificationToken}&email={email}'
                    style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: #ffffff; 
                    text-decoration: none; font-size: 16px; border-radius: 5px;' target='_blank'>Reset Password</a></p>
                <p>If you didn't request a password reset, you can ignore this email.</p>"));


            return true;
        }

        public bool UpdatePassword(string email, string token, string password)
        {
            using MySqlConnection conn = _database.GetConnection();
            try
            {
                conn.Open();
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                string query = "UPDATE users SET password=@password,remember_token=null WHERE email=@email AND remember_token=@token";
                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@password", hashedPassword);
                cmd.Parameters.AddWithValue("@token", token);
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected == 0)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Database Error: " + ex.Message);
                return false;
            }
            return true;
        }
        public PaginatedResponse GetUserListings(int page, int pageSize, string search, int userId, HttpRequest request)
        {
            List<Listing> listings = new List<Listing>();
            int totalCount = 0;

            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    int offset = (page - 1) * pageSize;

                    // Query to fetch total count (for pagination)
                    string countQuery = @"
                SELECT COUNT(*) FROM listings
                WHERE user_id = @userId 
                AND (@Search IS NULL OR @Search = '' OR title LIKE @Search OR tags LIKE @Search OR `desc` LIKE @Search);
            ";

                    using (MySqlCommand countCmd = new(countQuery, conn))
                    {
                        countCmd.Parameters.AddWithValue("@userId", userId);
                        countCmd.Parameters.AddWithValue("@Search", string.IsNullOrEmpty(search) ? "" : $"%{search}%");
                        totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    }

                    // Fetch paginated listings
                    string query = @"
                SELECT * FROM listings
                WHERE user_id = @userId 
                AND (@Search IS NULL OR @Search = '' OR title LIKE @Search OR tags LIKE @Search OR `desc` LIKE @Search)
                ORDER BY created_at DESC
                LIMIT @pageSize OFFSET @offset;
            ";

                    using (MySqlCommand cmd = new(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@Search", string.IsNullOrEmpty(search) ? "" : $"%{search}%");
                        cmd.Parameters.AddWithValue("@pageSize", pageSize);
                        cmd.Parameters.AddWithValue("@offset", offset);

                        using MySqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {

                            string baseUrl = $"{request.Scheme}://{request.Host}";
                            string profilePicturePath = reader.IsDBNull(reader.GetOrdinal("image")) ? "" : reader.GetString("image");
                            string physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", profilePicturePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                            string profilePicUrl;
                            if (!string.IsNullOrEmpty(profilePicturePath) && System.IO.File.Exists(physicalPath))
                            {
                                profilePicUrl = $"{baseUrl}/{profilePicturePath}";
                            }
                            else
                            {

                                profilePicUrl = $"{baseUrl}/uploads/listing_pictures/default-avatar.jpeg";
                            }

                            listings.Add(new Listing
                            {
                                Id = reader.GetInt32("id"),
                                UserId = reader.GetInt32("user_id"),
                                Title = reader.GetString("title"),
                                Desc = reader.GetString("desc"),
                                Tags = reader.GetString("tags"),
                                Email = reader.GetString("email"),
                                Link = reader.GetString("link"),
                                Image = profilePicUrl,
                                Approved = reader.GetInt32("approved"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                UpdatedAt = reader.GetDateTime("updated_at")
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database Error: " + ex.Message);
                    return new PaginatedResponse
                    {
                        Message = "Database error occurred.",
                        StatusCode = 500,
                        Data = new { listings = new List<Listing>() },
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalCount = 0,
                        TotalPages = 0,
                        HasPrevious = false,
                        HasNext = false
                    };
                }
            }

            // Pagination logic
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return new PaginatedResponse
            {
                Message = "Listings fetched successfully.",
                StatusCode = 200,
                Data = new { listings },
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPrevious = page > 1,
                HasNext = page < totalPages
            };
        }

    }
}

