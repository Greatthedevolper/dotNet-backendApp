using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using DotNetApi.Models;
using DotNetApi.Services;
using DotNetApi.Data;

namespace DotNetApi.Controllers
{
    [ApiController]
    [Route("api/listings")]
    public class ListingController : ControllerBase
    {
        private readonly ListingRepository _listingRepository;
        private readonly UserRepository _userRepository;
        private readonly EmailService _emailService;

        public ListingController(UserRepository userRepository, EmailService emailService, ListingRepository listingRepository)
        {
            _userRepository = userRepository;
            _listingRepository = listingRepository;
            _emailService = emailService;
        }


        [HttpGet]
        public ActionResult<object> GetListings([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = "")
        {
            var result = _listingRepository.GetAllListings(page, pageSize, search, HttpContext.Request);

            if (result == null || result.Data == null || ((dynamic)result.Data).listings.Count == 0)
            {
                return NotFound(new
                {
                    message = "No listings found.",
                    statusCode = 404,
                    data = new { listings = new List<Listing>(), top4Listings = new List<object>() },
                    currentPage = page,
                    pageSize = pageSize,
                    totalCount = 0,
                    totalPages = 0,
                    hasPrevious = false,
                    hasNext = false
                });
            }

            return Ok(result);
        }

        [HttpGet("/api/listing/{id}")]
        public ActionResult<object> GetListing(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid listing ID." });
            }

            var (listing, currentUser) = _listingRepository.GetSingleListing(id, Request);

            if (listing == null)
            {
                return NotFound(new { status = false, message = "No listing was found." });
            }

            return Ok(new
            {
                status = true,
                message = "Listing found.",
                data = new
                {
                    listing,
                    user = currentUser
                }
            });
        }

        [HttpPost]
        public IActionResult SaveListing([FromForm] ListingFormDto dto)
        {
            if (dto == null)
                return BadRequest(new { status = false, message = "Invalid data." });

            // Validate required fields for new listings
            if (dto.Id == 0 && dto.ImageFile == null && string.IsNullOrEmpty(dto.ExistingImage))
            {
                return BadRequest(new { status = false, message = "Image is required for new listings." });
            }

            // Convert DTO to Listing model
            var listing = new Listing
            {
                Id = dto.Id,
                UserId = dto.UserId,
                Title = dto.Title ?? string.Empty,
                Desc = dto.Desc ?? string.Empty,
                Tags = dto.Tags ?? string.Empty,
                Email = dto.Email ?? string.Empty,
                Link = dto.Link ?? string.Empty,
                Approved = dto.Approved
                // Image will be handled by the repository
            };

            try
            {
                bool isSaved = _listingRepository.SaveListing(
                    listing,
                    dto.ImageFile,
                    dto.ExistingImage
                );

                if (isSaved)
                {
                    return Ok(new
                    {
                        status = true,
                        message = listing.Id == 0 ? "Listing created successfully." : "Listing updated successfully."
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        status = false,
                        message = "An error occurred while saving the listing."
                    });
                }
            }
            catch (Exception ex)
            {
                // Log the exception here
                return StatusCode(500, new
                {
                    status = false,
                    message = "An unexpected error occurred.",
                    error = ex.Message // Only include in development environment
                });
            }
        }


        public class ListingFormDto
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string? Title { get; set; }
            public string? Desc { get; set; }
            public string? Tags { get; set; }
            public string? Email { get; set; }
            public string? Link { get; set; }
            public int Approved { get; set; }
            public IFormFile? ImageFile { get; set; }
            public string? ExistingImage { get; set; }
        }

        [HttpPut("/api/listing/approval")]
        public IActionResult ListingApproved(int Id, int Approved)
        {
            if (Id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid listing ID." });
            }

            bool isApproved = _listingRepository.ApprovedListing(Id, Approved);
            if (isApproved)
            {
                return Ok(new { status = true, message = "Listing status is updated" });
            }
            return BadRequest(new { status = false, message = "Invalid listing ID." });
        }
        [HttpDelete("/api/listing/{id}")]
        public IActionResult ListingDelete(int Id)
        {
            if (Id <= 0)
            {
                return BadRequest(new { status = false, message = "Invalid listing ID." });
            }

            bool isApproved = _listingRepository.DeleteListing(Id);
            if (isApproved)
            {
                return Ok(new { status = true, message = "Listing is deleted" });
            }
            return BadRequest(new { status = false, message = "Invalid listing ID." });
        }
    }
}
