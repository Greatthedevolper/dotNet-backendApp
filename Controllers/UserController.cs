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
        public IActionResult Register([FromBody] User newUser)
        {
            if (string.IsNullOrEmpty(newUser.Name) || string.IsNullOrEmpty(newUser.Email) || string.IsNullOrEmpty(newUser.Password))
            {
                return BadRequest(new { status = false, message = "Name, email, and password are required." });
            }

            var existingUser = _userRepository.GetUserByEmail(newUser.Email);
            if (existingUser != null)
            {
                return Conflict(new { status = false, message = "User already exists." });
            }

            var createdUser = _userRepository.AddUser(newUser.Name, newUser.Email, newUser.Password);

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
        [HttpPost("verify")]
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

            bool verified = _userRepository.VerifyUserEmail(model.Email, model.Token);
            if (!verified)
            {
                return Unauthorized(new { status = false, message = "Invalid or expired token." });
            }

            return Ok(new { status = true, message = "Email verified successfully." });
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("95c6ce46bc28fe3cad21b6460c30b92a"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, System.Guid.NewGuid().ToString()),
                new Claim("userId", user.Id.ToString()),
                new Claim("role", user.Role ?? "User")
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

    }
}



