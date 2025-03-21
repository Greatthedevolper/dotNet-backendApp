using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using DotNetApi.Models;
using BCrypt.Net;

namespace DotNetApi.Data
{
    public class ListingRepository
    {
        private readonly Database _database;

        public ListingRepository()
        {
            _database = new Database();
        }

        public PaginatedResponse GetAllListings(int page, int pageSize, string search)
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
                    string countQuery = @"
                SELECT COUNT(*) FROM listings
                WHERE (@Search IS NULL OR @Search = '' OR title LIKE @Search OR tags LIKE @Search OR `desc` LIKE @Search);";

                    using (MySqlCommand countCmd = new(countQuery, conn))
                    {
                        countCmd.Parameters.AddWithValue("@Search", $"%{search}%");
                        totalCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    }

                    // Fetch paginated listings
                    string query = @"
                SELECT * FROM listings
                WHERE (@Search IS NULL OR @Search = '' OR title LIKE @Search OR tags LIKE @Search OR `desc` LIKE @Search)
                LIMIT @pageSize OFFSET @offset;";

                    using (MySqlCommand cmd = new(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Search", $"%{search}%");
                        cmd.Parameters.AddWithValue("@pageSize", pageSize);
                        cmd.Parameters.AddWithValue("@offset", offset);

                        using MySqlDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            listings.Add(new Listing
                            {
                                Id = reader.GetInt32("id"),
                                UserId = reader.GetInt32("user_id"),
                                Title = reader.GetString("title"),
                                Desc = reader.GetString("desc"),
                                Tags = reader.GetString("tags"),
                                Email = reader.GetString("email"),
                                Link = reader.GetString("link"),
                                Image = reader.IsDBNull(reader.GetOrdinal("image")) ? null : reader.GetString("image"),
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
                }
            }

            // Pagination logic
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return new PaginatedResponse
            {
                Message = "Listings fetched successfully.",
                StatusCode = 200,
                Data = new
                {
                    listings,
                },
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

