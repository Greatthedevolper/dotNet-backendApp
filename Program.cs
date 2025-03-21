using DotNetApi.Services;
using DotNetApi.Data;
var builder = WebApplication.CreateBuilder(args);

// Enable CORS
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

// ✅ Register controllers
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<UserRepository>();  // ✅ Register UserRepository
builder.Services.AddScoped<EmailService>();

var app = builder.Build();

// ✅ Enable CORS before other middleware
app.UseCors("AllowNuxtFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// ✅ Map Controllers (this enables API endpoints)
app.MapControllers();

app.Run();
