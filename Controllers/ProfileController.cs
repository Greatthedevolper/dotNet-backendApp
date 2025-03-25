using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetApi.Services;
using DotNetApi.Models;
using DotNetApi.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DotNetApi.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class ProfileController(ProfileRepository profileRepository, EmailService emailService) : ControllerBase
    {
        private readonly ProfileRepository _profileRepository = profileRepository;
        private readonly EmailService _emailService = emailService;

        [Authorize]
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            var userId = User.FindFirstValue("user_id");
            var userEmail = User.FindFirstValue(ClaimTypes.Email);       // Get Email
            var userName = User.FindFirstValue(ClaimTypes.Name);         // Get Name
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            const string ProfilePictureClaimType = "profile_picture";
            var userPic = User.FindFirstValue(ProfilePictureClaimType);

            if (userId == null)
            {
                return Unauthorized(new { status = false, message = "User not authenticated." });
            }

            return Ok(new
            {
                status = true,
                user = new
                {
                    Id = userId,
                    Name = userName,
                    Email = userEmail,
                    Role = userRole,
                    ProfilePicture = $"http://localhost:5067{userPic}"
                }
            });
        }

        [Authorize]
        [HttpPost("profile/update-picture")]
        public async Task<IActionResult> UpdateProfilePicture([FromForm] ProfilePictureRequest model)
        {
            var userId = User.FindFirstValue("user_id");
            // var userId = 8.ToString();
            if (userId == null)
            {
                return Unauthorized(new { status = false, message = "User not authenticated." });
            }

            if (model.File == null || model.File.Length == 0)
            {
                return BadRequest(new { status = false, message = "File not provided." });
            }

            var allowedExtensions = new List<string> { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(model.File.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { status = false, message = "Invalid file type. Only .jpg, .jpeg, .png allowed." });
            }

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profile_pictures");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            var filePath = Path.Combine(uploadPath, uniqueFileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await model.File.CopyToAsync(stream);

            // Save file path in the database
            string dbFilePath = $"/uploads/profile_pictures/{uniqueFileName}";
            var result = _profileRepository.UpdateProfileImage(int.Parse(userId), dbFilePath);

            if (result)
            {
                return Ok(new { status = true, message = "Profile picture updated successfully." });
            }
            else
            {
                return BadRequest(new { status = false, message = "Failed to update profile picture." });
            }
        }

        static string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("95c6ce46bc28fe3cad21b6460c30b92a"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            const string ProfilePictureClaimType = "profile_picture";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email), // Subject is email
                new Claim(JwtRegisteredClaimNames.Jti, System.Guid.NewGuid().ToString()), // Unique token ID
                new Claim(ClaimTypes.NameIdentifier, user.Email), // Store email as NameIdentifier
                new Claim("user_id", user.Id.ToString()), // Store numeric user ID as a custom claim
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim(ProfilePictureClaimType, user.ProfilePicture ?? "")
            };

            var token = new JwtSecurityToken(
                issuer: "http://localhost:5067",
                audience: "http://localhost:4000",
                claims: claims,
                expires: System.DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class ProfilePictureRequest
        {
            public IFormFile? File { get; set; }
        }
    }
}
