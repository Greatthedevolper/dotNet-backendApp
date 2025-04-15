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
    public class ProfileController : ControllerBase
    {
        private readonly ProfileRepository _profileRepository;
        private readonly UserRepository _userRepository;
        private readonly EmailService _emailService;

        public ProfileController(ProfileRepository profileRepository, UserRepository userRepository, EmailService emailService)
        {
            _profileRepository = profileRepository;
            _userRepository = userRepository;
            _emailService = emailService;
        }

        [Authorize]
        [HttpGet("profile")]
        public IActionResult Profile()
        {
            var userIdClaim = User.FindFirstValue("user_id");
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { status = false, message = "User not authenticated." });
            }
            var currentUser = _userRepository.GetUserById(userId);

            if (currentUser == null)
            {
                return NotFound(new { status = false, message = "User not found." });
            }
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string profilePicturePath = currentUser.ProfilePicture ?? "";
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
            // string profilePicUrl = string.IsNullOrEmpty(currentUser.ProfilePicture)
            //     ? $"{baseUrl}/uploads/profile_pictures/default-avatar.jpeg"
            //     : $"{baseUrl}{currentUser.ProfilePicture}";

            return Ok(new
            {
                status = true,
                user = new
                {
                    Id = currentUser.Id,
                    Name = currentUser.Name,
                    Email = currentUser.Email,
                    Role = currentUser.Role,
                    Token = currentUser.Token,
                    EmailVerifiedAt = currentUser.EmailVerifiedAt,
                    ProfilePicture = profilePicUrl
                }
            });
        }

        [Authorize]
        [HttpPost("profile/update-picture")]
        public async Task<IActionResult> UpdateProfilePicture([FromForm] ProfilePictureRequest model)
        {
            var userIdString = User.FindFirstValue("user_id");
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { status = false, message = "User not authenticated." });
            }

            if (model.File == null || model.File.Length == 0)
            {
                return BadRequest(new { status = false, message = "File not provided." });
            }

            var allowedExtensions = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(model.File.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { status = false, message = "Invalid file type. Only .jpg, .jpeg, .png,.gif,.webp allowed." });
            }
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profile_pictures");
            Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";


            var newFilePath = Path.Combine(uploadsFolder, uniqueFileName);
            await using (var stream = new FileStream(newFilePath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream);
            }

            // Save file path in the database
            string dbFilePath = $"/uploads/profile_pictures/{uniqueFileName}";



            var oldPath = _profileRepository.GetExistingProfileImagePath(userId);
            var result = _profileRepository.UpdateProfileImage(userId, dbFilePath);

            if (result)
            {
                if (!string.IsNullOrWhiteSpace(oldPath) && !oldPath.Contains("default-avatar.jpg"))
                {
                    var fullOldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(fullOldPath))
                    {
                        System.IO.File.Delete(fullOldPath);
                    }
                }
                return Ok(new { status = true, message = "Profile picture updated successfully." });
            }
            else
            {
                if (System.IO.File.Exists(newFilePath))
                {
                    System.IO.File.Delete(newFilePath);
                }
                return BadRequest(new { status = false, message = "Failed to update profile picture." });
            }
        }

        [HttpPost("profile/update/{userid}")]
        public IActionResult UpdateProfile(int userid, [FromBody] ProfileUpdateRequest model)
        {
            if (userid <= 0)
            {
                return Unauthorized(new { status = false, message = "Invalid user ID. Authentication required." });
            }

            if (model == null)
            {
                return BadRequest(new { status = false, message = "Invalid request. No data provided." });
            }

            var currentUser = _userRepository.GetUserById(userid);
            if (currentUser == null)
            {
                return NotFound(new { status = false, message = "User not found." });
            }

            if (!string.IsNullOrWhiteSpace(model.Email) && model.Email != currentUser.Email)
            {
                var existingUser = _userRepository.GetUserByEmail(model.Email, HttpContext.Request);
                if (existingUser != null && existingUser.Id != userid)
                {
                    return Conflict(new { status = false, message = "This email is already in use by another user." });
                }
            }

            string newName = string.IsNullOrWhiteSpace(model.Name) ? currentUser.Name : model.Name;
            string newEmail = string.IsNullOrWhiteSpace(model.Email) ? currentUser.Email : model.Email;

            if (newName == currentUser.Name && newEmail == currentUser.Email)
            {
                return BadRequest(new { status = false, message = "No changes detected." });
            }

            bool isUpdated = _profileRepository.UpdateProfile(userid, newEmail, newName);
            if (!isUpdated)
            {
                return StatusCode(500, new { status = false, message = "Failed to update profile. Please try again later." });
            }

            return Ok(new { status = true, message = "Your profile has been updated successfully." });
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
    public class ProfileUpdateRequest
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
    }
}

