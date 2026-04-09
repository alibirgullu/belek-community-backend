using BelekCommunity.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; 
using System.Text;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<BelekCommunityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000") 
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});


var jwtSettings = builder.Configuration.GetSection("JwtSettings");

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


builder.Services.AddScoped<BelekCommunity.Api.Services.EmailService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IEventService, BelekCommunity.Api.Services.EventService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IAnnouncementService, BelekCommunity.Api.Services.AnnouncementService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IUserService, BelekCommunity.Api.Services.UserService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.IFileService, BelekCommunity.Api.Services.FileService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.ICommunityService, BelekCommunity.Api.Services.CommunityService>();
builder.Services.AddScoped<BelekCommunity.Api.Services.ICommunityMemberService, BelekCommunity.Api.Services.CommunityMemberService>();
builder.Services.AddHttpClient<BelekCommunity.Api.Services.IAiChatService, BelekCommunity.Api.Services.AiChatService>();
builder.Services.AddHttpClient<BelekCommunity.Api.Services.IPushNotificationService, BelekCommunity.Api.Services.PushNotificationService>();
builder.Services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles; });
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Belek Community API", Version = "v1" });

    
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


var app = builder.Build();



if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseCors("AllowReactApp");


app.UseAuthentication();


app.UseAuthorization();

app.MapControllers();

app.Run();
