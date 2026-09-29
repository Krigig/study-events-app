using System.Reflection;
using WebApi.Interfaces;
using WebApi.Middleware;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Контроллеры и единый формат ошибок (ProblemDetails)
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Бизнес-логика в сервисе, подключённом через DI.
// Singleton — данные хранятся в памяти приложения.
builder.Services.AddSingleton<IEventService, EventService>();

// Swagger: XML-комментарии из сборки попадают в описания эндпоинтов и схем
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Swagger UI доступен на /swagger только в окружении разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Глобальный обработчик исключений первым в pipeline — перехватывает
// необработанные исключения всех последующих компонентов
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseStatusCodePages(); // Единый формат ошибок (ProblemDetails) для пустых 4xx-ответов
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
