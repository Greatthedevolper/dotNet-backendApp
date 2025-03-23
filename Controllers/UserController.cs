using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetApi.Services;
using DotNetApi.Models;
using DotNetApi.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Authorization;

namespace DotNetApi.Controllers // ✅ Ensure correct namespace
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _userRepository;
        private readonly EmailService _emailService;

        public UserController(UserRepository userRepository, EmailService emailService)
        {
            _userRepository = userRepository;
            _emailService = emailService;
        }

        [HttpGet]
        public ActionResult<List<User>> GetUsers()
        {
            return Ok(_userRepository.GetAllUsers());
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest model)
        {
            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
            {
                return BadRequest(new { status = false, message = "Name, email, and password are required." });
            }

            var existingUser = _userRepository.GetUserByEmail(model.Email);
            if (existingUser != null)
            {
                return Conflict(new { status = false, message = "User already exists." });
            }

            var createdUser = _userRepository.AddUser(model.Name, model.Email, model.Password);

            if (createdUser == null)
            {
                return StatusCode(500, new { status = false, message = "User registration failed." });
            }

            return Ok(new { status = true, message = "You have successfully registered. Please check your email for verification." });

        }
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
            {
                return BadRequest(new { status = false, message = "Email and password are required." });
            }

            // Check if user exists
            var user = _userRepository.GetUserByEmail(model.Email);
            if (user == null)
            {
                return Unauthorized(new { status = false, message = "Email doesn't exist" });
            }
            if (user?.EmailVerifiedAt == null)
            {
                return Unauthorized(new { status = false, message = "Please verify your email before logging in." });
            }
            bool isValidPassword = _userRepository.VerifyPassword(model.Email, model.Password);
            if (!isValidPassword)
            {
                return Unauthorized(new { status = false, message = "Password is incorrect." });
            }

            // Generate JWT Token
            string token = GenerateJwtToken(user);

            return Ok(new { status = true, message = "Login successful.", token, user = user });
        }
        [HttpPost("verify-account")]
        public IActionResult VerifyUser([FromBody] VerifyRequest model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Token))
            {
                return BadRequest(new { status = false, message = "Email and Token are required." });
            }

            var user = _userRepository.GetUserByEmail(model.Email);
            if (user == null)
            {
                return Unauthorized(new { status = false, message = "Email doesn't exist." });
            }
            if (user?.EmailVerifiedAt != null)
            {
                return Ok(new { status = true, message = "Account is already verified" });
            }

            bool verified = _userRepository.VerifyUserEmail(model.Email, model.Token);
            if (!verified)
            {
                return Unauthorized(new { status = false, message = "Invalid or expired token." });
            }

            return Ok(new { status = true, message = "Email verified successfully." });
        }

        [HttpPost("forgot-password")]
        public IActionResult ForgetPassword([FromBody] ForgotPassword model)
        {
            if (string.IsNullOrEmpty(model.Email))
            {
                return BadRequest(new { status = false, message = "Email is required." });
            }
            var user = _userRepository.GetUserByEmail(model.Email);
            if (user == null)
            {
                return Unauthorized(new { status = false, message = "Email doesn't exist." });
            }
            if (user?.EmailVerifiedAt == null)
            {
                return Unauthorized(new { status = false, message = "please verify your email before resetting password." });
            }
            bool emailSent = _userRepository.SendResetPasswordEmail(model.Email);
            if (!emailSent)
            {
                return StatusCode(500, new { status = false, message = "Failed to send reset password email." });
            }
            return Ok(new { status = true, message = "Reset password link sent to your email." });
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest model)
        {
            if (string.IsNullOrEmpty(model.Email))
            {
                return BadRequest(new { status = false, message = "Email is required." });
            }
            var user = _userRepository.GetUserByEmail(model.Email);
            if (user == null)
            {
                return Unauthorized(new { status = false, message = "Email doesn't exist." });
            }
            if (user?.EmailVerifiedAt == null)
            {
                return Unauthorized(new { status = false, message = "please verify your email before resetting password." });
            }
            if (string.IsNullOrEmpty(model.Token))
            {
                return BadRequest(new { status = false, message = "Token is required." });
            }
            if (user?.Token != model.Token)
            {
                return Unauthorized(new { status = false, message = "Invalid or expired token." });
            }
            if (string.IsNullOrEmpty(model.NewPassword))
            {
                return BadRequest(new { status = false, message = "New Password is required." });
            }
            if (string.IsNullOrEmpty(model.ConfirmPassword))
            {
                return BadRequest(new { status = false, message = "Confirm Password is required." });
            }
            if (model.ConfirmPassword != model.NewPassword)
            {
                return BadRequest(new { status = false, message = "New Password And Confirm Password are not matched." });
            }

            bool updated = _userRepository.UpdatePassword(model.Email, model.Token, model.NewPassword);
            if (!updated)
            {
                return StatusCode(500, new { status = false, message = "Failed to reset password." });
            }
            return Ok(new { status = true, message = "password reset successfully" });
        }

        [Authorize]
        [HttpGet("profile")]

        public IActionResult Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get User ID
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
                    ProfilePicture = userPic
                }
            });
        }


        // [Authorize]
        [HttpGet("dashboard")]
        public IActionResult Dashboard([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = "")
        {

            var userEmail = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
            var userIdClaim = User.FindFirstValue("user_id");

            if (string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized(new { status = false, message = "User email or user ID is not available in JWT." });
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { status = false, message = "Invalid user ID format." });
            }

            var user = _userRepository.GetUserByEmail(userEmail);
            if (user == null)
            {
                return Unauthorized(new { status = false, message = "Email is not registered." });
            }

            if (user.Id != userId)
            {
                return Unauthorized(new { status = false, message = "User ID mismatch." });
            }

            if (!string.Equals(user.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { status = false, message = "You are not authorized to access this resource." });
            }

            var result = _userRepository.GetUserListings(page, pageSize, search, userId);
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

        public class RegisterRequest
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Password { get; set; }
        }
        public class LoginRequest
        {
            public string? Email { get; set; }
            public string? Password { get; set; }
        }
        public class VerifyRequest
        {
            public string? Email { get; set; }
            public string? Token { get; set; }
        }
        public class ResetPasswordRequest
        {
            public string? Email { get; set; }
            public string? Token { get; set; }
            public string? NewPassword { get; set; }
            public string? ConfirmPassword { get; set; }
        }
        public class ForgotPassword
        {
            public string? Email { get; set; }
        }
        public class GetListing
        {
            public int UserId { get; set; }
        }

    }
}



