using BelekCommunity.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Swagger yetkilendirme modelleri için gerekli
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabaný Baðlantýsý
builder.Services.AddDbContext<BelekCommunityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// 2. CORS Ayarý (React Baðlantýsý Ýçin Þart)
// Tarayýcý güvenliðini aþmak ve React'in API'ye eriþmesine izin vermek için.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // React genelde bu portlarda çalýþýr
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// 3. JWT Authentication Ayarlarý
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
// Eðer appsettings.json'da key yoksa hata vermesin diye varsayýlan bir key (Geliþtirme için)
var secretKeyString = jwtSettings["SecretKey"] ?? "VarsayilanGizliAnahtar";
var secretKey = Encoding.UTF8.GetBytes(secretKeyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

// 4. Standart Servisler
builder.Services.AddScoped<BelekCommunity.Api.Services.EmailService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IEventService, BelekCommunity.Api.Services.EventService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IAnnouncementService, BelekCommunity.Api.Services.AnnouncementService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IUserService, BelekCommunity.Api.Services.UserService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IFileService, BelekCommunity.Api.Services.FileService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.ICommunityService, BelekCommunity.Api.Services.CommunityService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.ICommunityMemberService, BelekCommunity.Api.Services.CommunityMemberService>();
builder.Services.AddHttpClient<BelekCommunity.Api.Services.IAiChatService, BelekCommunity.Api.Services.AiChatService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- 5. SWAGGER GÜNCELLEMESÝ (JWT BEARER DESTEÐÝ) ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Belek Community API", Version = "v1" });

    // Swagger'a "Authorize" butonu ekliyoruz
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Token'ýnýzý girerken baþýna 'Bearer ' yazmayý unutmayýn. Örnek: Bearer eyJhbGci...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});
// ----------------------------------------------------

var app = builder.Build();

// --- MIDDLEWARE (SIRALAMA ÇOK ÖNEMLÝDÝR) ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 1. Önce CORS (React'e kapýyý aç)
app.UseCors("AllowReactApp");

// 2. Sonra Kimlik Doðrulama (Kimsin?)
app.UseAuthentication();

// 3. Sonra Yetki Kontrolü (Yetkin var mý?)
app.UseAuthorization();

app.MapControllers();

app.Run();