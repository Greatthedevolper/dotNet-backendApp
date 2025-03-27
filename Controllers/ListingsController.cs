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
            var result = _listingRepository.GetAllListings(page, pageSize, search);

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

            // ✅ Fix tuple handling
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
    }
}
