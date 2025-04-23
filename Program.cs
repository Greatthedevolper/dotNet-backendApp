using DotNetApi.Services;
using DotNetApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

// ✅ Register Dependencies
builder.Services.AddScoped<ListingRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ProfileRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<EmailService>();

// ✅ Configure Swagger for JWT Authentication
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Listing API",
        Version = "v1",
        Description = "This is the API for my Nuxt + .NET full-stack app about listings."
    });

    // 🔹 Enable JWT authentication in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {your_token_here}' to authenticate.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
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
