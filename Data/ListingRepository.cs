using System;
using System.Collections.Generic;
using BCrypt.Net;
using DotNetApi.Models;
using MySqlConnector;
using Org.BouncyCastle.Asn1.Ocsp;

namespace DotNetApi.Data
{
    public class ListingRepository
    {
        private readonly Database _database;
        private readonly UserRepository _userRepository;
        private readonly CategoryRepository _categoryRepository;

        public ListingRepository(
            UserRepository userRepository,
            CategoryRepository categoryRepository
        )
        {
            _database = new Database();
            _userRepository = userRepository;
            _categoryRepository = categoryRepository;
        }

        public PaginatedResponse GetAllListings(
            int page,
            int pageSize,
            string search,
            string order,
            string orderBy,
            HttpRequest request
        )
        {
            List<Listing> listings = [];
            int totalCount = 0;

            using (MySqlConnection conn = _database.GetConnection())
            {
                try
                {
                    conn.Open();
                    int offset = (page - 1) * pageSize;

                    // Query to fetch total count (for pagination)
                    string countQuery =
                        @"
                SELECT COUNT(*) FROM listings
                WHERE (@Search IS NULL OR @Search = '' OR title LIKE @Search OR tags LIKE @Search OR `desc` LIKE @Search);";

                    using (MySqlCommand countCmd = new(countQuery, conn))
                    {
                        countCmd.Parameters.AddWithValue("@Search", $"%{search}%");
                        totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    }

                    string orderClause = order.ToUpper() == "DESC" ? "DESC" : "ASC";
                    string query =
                        $@"
                        SELECT * FROM listings
                        WHERE (@Search IS NULL OR @Search = '' OR title LIKE @Search OR tags LIKE @Search OR `desc` LIKE @Search)
                        ORDER BY {orderBy} {orderClause}
                        LIMIT @pageSize OFFSET @offset;";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@Search", $"%{search}%");
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);
                    cmd.Parameters.AddWithValue("@offset", offset);

                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string baseUrl = $"{request.Scheme}://{request.Host}";

                        string? imagePath = reader.IsDBNull(reader.GetOrdinal("image"))
                            ? null
                            : reader.GetString("image");

                        string profilePicUrl = string.IsNullOrWhiteSpace(imagePath)
                            ? $"{baseUrl}/uploads/listing_pictures/default-avatar.jpeg"
                            : $"{baseUrl}/{imagePath}";
                        listings.Add(
                            new Listing
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
                HasNext = page < totalPages,
            };
        }

        public (Listing? listing, User? currentUser, Category? currentCategory) GetSingleListing(
            int id,
            HttpRequest request
        )
        {
            Listing? listing = null;
            using (MySqlConnection conn = _database.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM listings WHERE id=@id LIMIT 1";
                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@id", id);
                using MySqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string baseUrl = $"{request.Scheme}://{request.Host}";

                    string? imagePath = reader.IsDBNull(reader.GetOrdinal("image"))
                        ? null
                        : reader.GetString("image");

                    string profilePicUrl = string.IsNullOrWhiteSpace(imagePath)
                        ? $"{baseUrl}/uploads/listing_pictures/default-avatar.jpeg"
                        : $"{baseUrl}/{imagePath}";
                    listing = new Listing
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
                        CategoryId = reader.IsDBNull(reader.GetOrdinal("category_id"))
                            ? null
                            : reader.GetInt32(reader.GetOrdinal("category_id")),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at"),
                    };
                }
            }
            User? currentUser =
                listing != null ? _userRepository.GetUserById(listing.UserId) : null;
            Category? currentCategory =
                listing != null && listing.CategoryId.HasValue
                    ? _categoryRepository.GetSingleCategory(listing.CategoryId.Value)
                    : null;

            return (listing, currentUser, currentCategory);
        }

        public bool SaveListing(
            Listing listing,
            IFormFile? imageFile = null,
            string? existingImage = null
        )
        {
            using MySqlConnection conn = _database.GetConnection();
            conn.Open();

            string? imagePath = null;

            // Handle image upload/retention
            if (imageFile != null && imageFile.Length > 0)
            {
                // New image uploaded - process it
                string uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "listing_pictures"
                );
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName =
                    Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    imageFile.CopyTo(stream);
                }

                imagePath = $"uploads/listing_pictures/{uniqueFileName}";

                // Delete old image if it exists
                if (listing.Id != 0) // Only for updates
                {
                    string? oldImagePath = null;
                    string selectQuery = "SELECT image FROM listings WHERE id = @id LIMIT 1;";
                    using (MySqlCommand selectCmd = new(selectQuery, conn))
                    {
                        selectCmd.Parameters.AddWithValue("@id", listing.Id);
                        using var reader = selectCmd.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(reader.GetOrdinal("image")))
                        {
                            oldImagePath = reader.GetString("image");
                        }
                    }

                    if (!string.IsNullOrEmpty(oldImagePath))
                    {
                        string fullOldImagePath = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            oldImagePath.Replace("/", Path.DirectorySeparatorChar.ToString())
                        );
                        if (File.Exists(fullOldImagePath))
                        {
                            File.Delete(fullOldImagePath);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(existingImage))
            {
                // No new image uploaded, but existing image provided - keep it
                imagePath = existingImage;
            }
            // else - no image handling needed, will be set to NULL

            if (listing.Id == 0)
            {
                // CREATE listing
                string insertQuery =
                    @"
        INSERT INTO listings (user_id, title, `desc`, tags, email, link, image, approved,category_id, created_at, updated_at)
        VALUES (@userId, @title, @desc, @tags, @email, @link, @image, @approved,@categoryId, NOW(), NOW());";

                using MySqlCommand cmd = new(insertQuery, conn);
                cmd.Parameters.AddWithValue("@userId", listing.UserId);
                cmd.Parameters.AddWithValue("@title", listing.Title);
                cmd.Parameters.AddWithValue("@desc", listing.Desc);
                cmd.Parameters.AddWithValue("@tags", listing.Tags);
                cmd.Parameters.AddWithValue("@email", listing.Email);
                cmd.Parameters.AddWithValue("@link", listing.Link);
                cmd.Parameters.AddWithValue("@image", imagePath ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@approved", listing.Approved);
                cmd.Parameters.AddWithValue(
                    "@categoryId",
                    listing.CategoryId ?? (object)DBNull.Value
                );

                return cmd.ExecuteNonQuery() > 0;
            }
            else
            {
                // UPDATE listing
                string updateQuery =
                    @"
        UPDATE listings
        SET title = @title, `desc` = @desc, tags = @tags, email = @email, link = @link, 
            image = @image,category_id=@categoryId, updated_at = NOW()
        WHERE id = @id;";

                using MySqlCommand cmd = new(updateQuery, conn);
                cmd.Parameters.AddWithValue("@id", listing.Id);
                cmd.Parameters.AddWithValue("@title", listing.Title);
                cmd.Parameters.AddWithValue("@desc", listing.Desc);
                cmd.Parameters.AddWithValue("@tags", listing.Tags);
                cmd.Parameters.AddWithValue("@email", listing.Email);
                cmd.Parameters.AddWithValue("@link", listing.Link);
                cmd.Parameters.AddWithValue("@image", imagePath ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue(
                    "@categoryId",
                    listing.CategoryId ?? (object)DBNull.Value
                );

                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool ApprovedListing(int id, int approval)
        {
            using MySqlConnection conn = _database.GetConnection();
            conn.Open();
            string approvalQuery = "UPDATE listings SET approved = @approved WHERE id = @id;";
            using MySqlCommand cmd = new(approvalQuery, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@approved", approval);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool DeleteListing(int id)
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
