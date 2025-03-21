using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using DotNetApi.Models;
using DotNetApi.Data;

namespace DotNetApi.Controllers
{
    [ApiController]
    [Route("api/listings")]
    public class ListingController : ControllerBase
    {
        private readonly ListingRepository _listingRepository;

        public ListingController()
        {
            _listingRepository = new ListingRepository();
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


    }
}
