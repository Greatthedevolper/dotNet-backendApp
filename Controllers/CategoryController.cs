using System.Collections.Generic;
using DotNetApi.Data;
using DotNetApi.Models;
using DotNetApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetApi.Controllers
{
    [ApiController]
    [Route("api/categories")]
    public class CategoryController : ControllerBase
    {
        private readonly CategoryRepository _categoryRepository;
        private readonly EmailService _emailService;

        public CategoryController(EmailService emailService, CategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
            _emailService = emailService;
        }

        [HttpGet]
        public ActionResult<object> GetListings(string search = "")
        {
            var result = _categoryRepository.GetAllCategories(search, HttpContext.Request);

            if (result?.Count == 0 || result == null)
            {
                return NotFound(
                    new
                    {
                        message = "No listings found.",
                        statusCode = 404,
                        data = new { categories = new List<Category>() },
                    }
                );
            }

            return Ok(result);
        }

        [HttpPost]
        public IActionResult CreateCategory([FromQuery] string name, string description)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest(new { status = false, message = "category name is required." });
            }
            if (string.IsNullOrEmpty(description))
            {
                return BadRequest(
                    new { status = false, message = "category description is required." }
                );
            }
            var isAdded = _categoryRepository.SaveCategory(name, description);

            if (isAdded)
            {
                return Ok(new { status = true, message = "Category is created successfully" });
            }

            return StatusCode(500, new { status = false, message = "Category creation failed." });
        }
    }
}
