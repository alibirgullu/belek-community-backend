var builder = WebApplication.CreateBuilder(args);

// Controller desteði
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Controller’larý map et (EN KRÝTÝK SATIR)
app.MapControllers();

app.Run();
