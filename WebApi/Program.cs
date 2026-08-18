using WebApi.Interfaces;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Контроллеры и единый формат ошибок (ProblemDetails)
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Бизнес-логика в сервисе, подключённом через DI.
// Singleton — данные хранятся в памяти приложения.
builder.Services.AddSingleton<IEventService, EventService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger UI доступен на /swagger
app.UseSwagger();
app.UseSwaggerUI();

app.UseStatusCodePages(); // Единый формат ошибок (ProblemDetails) для пустых 4xx-ответов
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
