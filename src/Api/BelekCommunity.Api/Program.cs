using BelekCommunity.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Swagger yetkilendirme modelleri için gerekli
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
builder.Services.AddDbContext<BelekCommunityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// 2. CORS Ayarı (React Bağlantısı İçin Şart)
// Tarayıcı güvenliğini aşmak ve React'in API'ye erişmesine izin vermek için.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // React genelde bu portlarda çalışır
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// 3. JWT Authentication Ayarları
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtValidationErrors = ValidateJwtSettings(jwtSettings);

if (jwtValidationErrors.Count > 0)
{
    throw new InvalidOperationException(
        "JWT yapılandırması geçersiz. Lütfen appsettings veya environment değişkenlerini düzeltin:\n"
        + string.Join("\n", jwtValidationErrors.Select(error => $"- {error}")));
}

var secretKeyString = jwtSettings["SecretKey"]!;
var issuer = jwtSettings["Issuer"]!;
var audience = jwtSettings["Audience"]!;
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
        ValidIssuer = issuer,
        ValidAudience = audience,
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- 5. SWAGGER GÜNCELLEMESİ (JWT BEARER DESTEĞİ) ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Belek Community API", Version = "v1" });

    // Swagger'a "Authorize" butonu ekliyoruz
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Token'ınızı girerken başına 'Bearer ' yazmayı unutmayın. Örnek: Bearer eyJhbGci...",
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

// --- MIDDLEWARE (SIRALAMA ÇOK ÖNEMLİDİR) ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 1. Önce CORS (React'e kapıyı aç)
app.UseCors("AllowReactApp");

// 2. Sonra Kimlik Doğrulama (Kimsin?)
app.UseAuthentication();

// 3. Sonra Yetki Kontrolü (Yetkin var mı?)
app.UseAuthorization();

app.MapControllers();

app.Run();

static List<string> ValidateJwtSettings(IConfigurationSection jwtSettings)
{
    const int minimumSecretLengthInBytes = 32;
    var errors = new List<string>();

    var secretKey = jwtSettings["SecretKey"];
    var issuer = jwtSettings["Issuer"];
    var audience = jwtSettings["Audience"];
    var durationInMinutesRaw = jwtSettings["DurationInMinutes"];

    if (string.IsNullOrWhiteSpace(secretKey))
    {
        errors.Add("JwtSettings:SecretKey değeri zorunludur.");
    }
    else
    {
        var secretKeyBytes = Encoding.UTF8.GetByteCount(secretKey);
        if (secretKeyBytes < minimumSecretLengthInBytes)
        {
            errors.Add($"JwtSettings:SecretKey en az {minimumSecretLengthInBytes} byte olmalıdır.");
        }

        if (secretKey.Any(char.IsWhiteSpace))
        {
            errors.Add("JwtSettings:SecretKey boşluk karakteri içermemelidir.");
        }

        if (!Regex.IsMatch(secretKey, "^[A-Za-z0-9_\\-]+$"))
        {
            errors.Add("JwtSettings:SecretKey yalnızca harf, rakam, alt çizgi (_) ve tire (-) içermelidir.");
        }
    }

    if (string.IsNullOrWhiteSpace(issuer))
    {
        errors.Add("JwtSettings:Issuer değeri zorunludur.");
    }

    if (string.IsNullOrWhiteSpace(audience))
    {
        errors.Add("JwtSettings:Audience değeri zorunludur.");
    }

    if (!int.TryParse(durationInMinutesRaw, out var durationInMinutes) || durationInMinutes <= 0)
    {
        errors.Add("JwtSettings:DurationInMinutes pozitif bir tam sayı olmalıdır.");
    }

    return errors;
}
