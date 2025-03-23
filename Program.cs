using DotNetApi.Services;
using DotNetApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNuxtFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4000") // Allow only Nuxt frontend
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // If using authentication
    });
});

// ✅ Add Authentication & JWT Configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "http://localhost:5067",  // ✅ Must match token issuer
            ValidAudience = "http://localhost:4000", // ✅ Must match token audience
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("95c6ce46bc28fe3cad21b6460c30b92a")) // ✅ Must match secret key
        };
    });

builder.Services.AddAuthorization();

// ✅ Register controllers
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ Register Dependencies
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<EmailService>();

// ✅ Swagger Configuration

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Listing API",
        Version = "v1",
        Description = "This is the API for my Nuxt + .NET full-stack app about listings."
    });
});

var app = builder.Build();

// ✅ Enable Authentication & Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();

// ✅ Enable CORS before other middleware
app.UseCors("AllowNuxtFrontend");
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.InjectStylesheet("/css/swagger-ui-dark.css"); // ✅ Load dark mode CSS
    });
}




// ✅ Map Controllers (this enables API endpoints)
app.MapControllers();

app.Run();
